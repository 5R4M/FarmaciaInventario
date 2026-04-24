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

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.SKU == sku && p.StockQuantity > 0);

            if (product == null)
                return NotFound();

            return Json(new { id = product.Id, name = product.Name, price = product.Price, stock = product.StockQuantity });
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
                    TotalAmount = model.Items.Sum(i => i.Quantity * i.Price)
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in model.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.StockQuantity < item.Quantity)
                        throw new Exception($"Stock insuficiente para {product?.Name ?? "el producto"}");

                    // Update stock
                    product.StockQuantity -= item.Quantity;
                    
                    var detail = new SaleDetail
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    };
                    _context.SaleDetails.Add(detail);

                    // Log inventory transaction
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
        public async Task<IActionResult> Reports(DateTime? date, string shift)
        {
            var targetDate = date ?? DateTime.Today;
            var sales = _context.Sales.Include(s => s.SaleDetails).Where(s => s.SaleDate.Date == targetDate.Date);

            if (!string.IsNullOrEmpty(shift))
            {
                switch (shift)
                {
                    case "Mañana":
                        sales = sales.Where(s => s.SaleDate.Hour >= 6 && s.SaleDate.Hour < 14);
                        break;
                    case "Tarde":
                        sales = sales.Where(s => s.SaleDate.Hour >= 14 && s.SaleDate.Hour < 22);
                        break;
                    case "Noche":
                        sales = sales.Where(s => s.SaleDate.Hour >= 22 || s.SaleDate.Hour < 6);
                        break;
                }
            }

            ViewBag.Date = targetDate.ToString("yyyy-MM-dd");
            ViewBag.Shift = shift;
            return View(await sales.ToListAsync());
        }
    }

    public class SaleViewModel
    {
        public string CustomerName { get; set; } = string.Empty;
        public List<SaleItemViewModel> Items { get; set; } = new();
    }

    public class SaleItemViewModel
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
