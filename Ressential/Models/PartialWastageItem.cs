using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(WastageItemsMetadata))]
    public partial class WastageItem
    {
    }

    public class WastageItemsMetadata
    {
        [Required(ErrorMessage = "Wastage Item Date is required")]
        public DateTime WastageItemDate { get; set; }

      

        [StringLength(255)]
        public string Description { get; set; }

       
    }
}
