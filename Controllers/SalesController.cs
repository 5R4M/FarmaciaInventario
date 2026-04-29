using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FarmaciaInventario.Data;
using FarmaciaInventario.Models;
using Microsoft.AspNetCore.Authorization;

namespace FarmaciaInventario.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Sales.OrderByDescending(s => s.SaleDate).Take(20).ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        public async Task<IActionResult> SearchProduct(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return NotFound();

            // Exact SKU match — fastest path
            var exact = await _context.Products
                .Where(p => p.SKU == sku && p.StockQuantity > 0)
                .Select(p => new { p.Id, p.Name, p.Price, p.StockQuantity })
                .FirstOrDefaultAsync();

            if (exact != null)
                return Json(new { type = "single", id = exact.Id, name = exact.Name, price = exact.Price, stockQuantity = exact.StockQuantity });

            // Fallback: partial SKU or name contains search
            var matches = await _context.Products
                .Where(p => p.StockQuantity > 0 &&
                       (p.SKU.Contains(sku) || p.Name.Contains(sku)))
                .Select(p => new { p.Id, p.Name, p.Price, p.StockQuantity })
                .Take(10)
                .ToListAsync();

            return matches.Count switch
            {
                0 => NotFound(),
                1 => Json(new { type = "single", id = matches[0].Id, name = matches[0].Name, price = matches[0].Price, stockQuantity = matches[0].StockQuantity }),
                _ => Json(new { type = "multiple", results = matches })
            };
        }

        [HttpPost]
        public async Task<IActionResult> CreateSale([FromBody] SaleViewModel model)
        {
            if (model == null || model.Items == null || !model.Items.Any())
                return BadRequest("Venta vacía.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = new Sale
                {
                    SaleDate = DateTime.Now,
                    CustomerName = string.IsNullOrEmpty(model.CustomerName) ? "Consumidor Final" : model.CustomerName,
                    CustomerNit = string.IsNullOrEmpty(model.CustomerNit) ? "CF" : model.CustomerNit.ToUpper(),
                    CustomerAddress = model.CustomerAddress ?? string.Empty,
                    TotalAmount = model.Items.Sum(i => i.Quantity * i.Price)
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in model.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.StockQuantity < item.Quantity)
                        throw new Exception($"Stock insuficiente para {product?.Name ?? "el producto"}");

                    product.StockQuantity -= item.Quantity;

                    _context.SaleDetails.Add(new SaleDetail
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    });

                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        ProductId = item.ProductId,
                        Type = TransactionType.Out,
                        Quantity = item.Quantity,
                        Remarks = $"Venta #{sale.Id}"
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { saleId = sale.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSale(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = await _context.Sales
                    .Include(s => s.SaleDetails)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (sale == null) return NotFound();

                // Restore stock for each item
                foreach (var detail in sale.SaleDetails)
                {
                    var product = await _context.Products.FindAsync(detail.ProductId);
                    if (product != null)
                        product.StockQuantity += detail.Quantity;
                }

                _context.SaleDetails.RemoveRange(sale.SaleDetails);
                _context.Sales.Remove(sale);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(ex.Message);
            }
        }

        public async Task<IActionResult> Receipt(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.SaleDetails)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null) return NotFound();

            return View(sale);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reports(string date, string shift)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var targetDate = DateTime.TryParseExact(date, "yyyy-MM-dd", culture,
                             System.Globalization.DateTimeStyles.None, out var parsed)
                             ? parsed : DateTime.Today;

            var dayStart = targetDate.Date;
            var dayEnd = dayStart.AddDays(1);

            var salesQuery = _context.Sales
                .Include(s => s.SaleDetails)
                .Where(s => s.SaleDate >= dayStart && s.SaleDate < dayEnd);

            if (!string.IsNullOrEmpty(shift))
            {
                switch (shift)
                {
                    case "Mañana":
                        var mS = dayStart.AddHours(6);
                        var mE = dayStart.AddHours(14);
                        salesQuery = salesQuery.Where(s => s.SaleDate >= mS && s.SaleDate < mE);
                        break;
                    case "Tarde":
                        var tS = dayStart.AddHours(14);
                        var tE = dayStart.AddHours(22);
                        salesQuery = salesQuery.Where(s => s.SaleDate >= tS && s.SaleDate < tE);
                        break;
                    case "Noche":
                        var nS = dayStart.AddHours(22);
                        var nE = dayStart.AddHours(6);
                        salesQuery = salesQuery.Where(s => s.SaleDate >= nS || s.SaleDate < nE);
                        break;
                }
            }

            var esCulture = new System.Globalization.CultureInfo("es-ES");
            ViewBag.Date = targetDate.ToString("yyyy-MM-dd");
            ViewBag.Shift = shift ?? "";
            ViewBag.DisplayDate = targetDate.ToString("dd 'de' MMMM 'de' yyyy", esCulture);
            ViewBag.DisplayShift = string.IsNullOrEmpty(shift) ? "Todos los turnos" : shift;
            return View(await salesQuery.OrderBy(s => s.SaleDate).ToListAsync());
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReportPdf(string date, string shift)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var targetDate = DateTime.TryParseExact(date, "yyyy-MM-dd", culture,
                             System.Globalization.DateTimeStyles.None, out var parsed)
                             ? parsed : DateTime.Today;

            var dayStart = targetDate.Date;
            var dayEnd = dayStart.AddDays(1);

            var salesQuery = _context.Sales
                .Include(s => s.SaleDetails)
                .ThenInclude(d => d.Product)
                .Where(s => s.SaleDate >= dayStart && s.SaleDate < dayEnd);

            if (!string.IsNullOrEmpty(shift))
            {
                switch (shift)
                {
                    case "Mañana":
                        salesQuery = salesQuery.Where(s => s.SaleDate >= dayStart.AddHours(6) && s.SaleDate < dayStart.AddHours(14));
                        break;
                    case "Tarde":
                        salesQuery = salesQuery.Where(s => s.SaleDate >= dayStart.AddHours(14) && s.SaleDate < dayStart.AddHours(22));
                        break;
                    case "Noche":
                        salesQuery = salesQuery.Where(s => s.SaleDate >= dayStart.AddHours(22) || s.SaleDate < dayStart.AddHours(6));
                        break;
                }
            }

            var esCulture = new System.Globalization.CultureInfo("es-ES");
            ViewBag.DisplayDate = targetDate.ToString("dd 'de' MMMM 'de' yyyy", esCulture);
            ViewBag.DisplayShift = string.IsNullOrEmpty(shift) ? "Todos los turnos" : shift;
            ViewBag.GeneratedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            return View(await salesQuery.OrderBy(s => s.SaleDate).ToListAsync());
        }
    }

    public class SaleViewModel
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerNit { get; set; } = "CF";
        public string CustomerAddress { get; set; } = string.Empty;
        public List<SaleItemViewModel> Items { get; set; } = new();
    }

    public class SaleItemViewModel
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}