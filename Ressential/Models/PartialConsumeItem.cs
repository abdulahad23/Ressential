using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(ConsumeItemsMetadata))]
    public partial class ConsumeItem
    {
    }

    public class ConsumeItemsMetadata
    {
        [Required(ErrorMessage = "Consume Item Date is required")]
        public DateTime ConsumeItemDate { get; set; }

           
        [StringLength(255)]
        public string Description { get; set; }

       
    }
}
