using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FarmaciaInventario.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string SKU { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Display(Name = "Stock Quantity")]
        public int StockQuantity { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Expiration Date")]
        public DateTime? ExpirationDate { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        public Category? Category { get; set; }
    }
}
