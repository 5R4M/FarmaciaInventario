using System.ComponentModel.DataAnnotations;

namespace FarmaciaInventario.Models
{
    public enum TransactionType
    {
        In,
        Out,
        Adjustment
    }

    public class InventoryTransaction
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        public Product? Product { get; set; }

        [Required]
        public TransactionType Type { get; set; }

        [Required]
        public int Quantity { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime Date { get; set; } = DateTime.Now;

        public string Remarks { get; set; } = string.Empty;
    }
}
