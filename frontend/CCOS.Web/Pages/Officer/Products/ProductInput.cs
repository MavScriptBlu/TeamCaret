using System.ComponentModel.DataAnnotations;

namespace CCOS.Web.Pages.Officer.Products
{
    /// <summary>
    /// Input and validation for creating  and editing a new product
    /// implements IValidatableObject to validate that the regular price is not less than the member price
    /// </summary>
    public sealed class ProductInput : IValidatableObject
    {
        [Required]
        [StringLength(30)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "10000.00", ErrorMessage = "The regular price must be between 0 and 10000.00")]
        [Display(Name = "Regular Price")]
        public decimal? Price { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "10000.00", ErrorMessage = "The member price must be between 0 and 10000.00")]
        [Display(Name = "Member Price")]
        public decimal? MemberPrice { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "The stock quantity must be 0 or greater")]
        [Display(Name = "Stock Quantity")]
        public int? StockQuantity { get; set; }

        // New products are active by default, not shown on the form
        public bool IsActive { get; set; } = true;


        // Implement IValidatableObject to validate that the regular price is not less than the member price
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Price is decimal regularPrice && MemberPrice is decimal memberPrice && memberPrice > regularPrice)
            {
                yield return new ValidationResult("the member price cannot be greater than the regular price", new[] { nameof(MemberPrice) });
            }
        }
    }
}
