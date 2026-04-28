using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FarmaciaInventario.Models
{
    public class Sale
    {
        public int Id { get; set; }

        [Required]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public string CustomerName { get; set; } = "Consumidor Final";

        [StringLength(20)]
        public string CustomerNit { get; set; } = "CF";

        [StringLength(200)]
        public string CustomerAddress { get; set; } = string.Empty;

        public ICollection<SaleDetail> SaleDetails { get; set; } = new List<SaleDetail>();
    }

    public class SaleDetail
    {
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }
        public Sale? Sale { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal => Quantity * UnitPrice;
    }
}
