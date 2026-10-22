using System.ComponentModel.DataAnnotations;

namespace CCOS.Web.Pages.Officer.Products
{
    /// <summary>
    /// The values typed into the product form. Used by both the Create and Edit pages.
    /// The attributes on each property (Required, StringLength, Range) are the validation rules.
    /// The sizes match the Product columns in the database (see AppDbContext).
    /// It also implements IValidatableObject for the one rule that compares two fields:
    /// the member price can't be higher than the regular price.
    /// </summary>
    public sealed class ProductInput : IValidatableObject
    {
        // Built once instead of on every validation run.
        private static readonly string[] MemberPriceFieldNames = [nameof(MemberPrice)];

        /// <summary>Which group the product belongs to, like Swag. Up to 30 characters.</summary>
        [Required]
        [StringLength(30)]
        public string Category { get; set; } = string.Empty;

        /// <summary>The product name. Up to 100 characters.</summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description. Up to 500 characters.</summary>
        [StringLength(500)]
        public string? Description { get; set; }

        // Prices and stock are nullable on purpose: a blank box means "not typed yet" (Required
        // then catches it) instead of quietly becoming 0.

        /// <summary>The regular price, from 0 to 10000.00.</summary>
        [Required]
        [Range(typeof(decimal), "0", "10000.00", ErrorMessage = "The regular price must be between 0 and 10000.00")]
        [Display(Name = "Regular Price")]
        public decimal? Price { get; set; }

        /// <summary>The club member price, from 0 to 10000.00. Can't be higher than the regular price.</summary>
        [Required]
        [Range(typeof(decimal), "0", "10000.00", ErrorMessage = "The member price must be between 0 and 10000.00")]
        [Display(Name = "Member Price")]
        public decimal? MemberPrice { get; set; }

        /// <summary>How many are on hand. 0 or more.</summary>
        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "The stock quantity must be 0 or greater")]
        [Display(Name = "Stock Quantity")]
        public int? StockQuantity { get; set; }

        /// <summary>
        /// False means the product is deactivated (hidden from customers). New products are active
        /// by default and the Create form doesn't show this box; Edit carries the saved value through.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Runs after the single-field rules pass. Adds an error on MemberPrice when it is
        /// higher than the regular price.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Only compare when both prices were typed. A blank one is already reported by Required.
            if (Price is decimal regularPrice && MemberPrice is decimal memberPrice && memberPrice > regularPrice)
            {
                yield return new ValidationResult("the member price cannot be greater than the regular price", MemberPriceFieldNames);
            }
        }
    }
}
