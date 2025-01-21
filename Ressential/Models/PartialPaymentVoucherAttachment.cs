using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(PaymentVoucherAttachmentMetadata))]
    public partial class PaymentVoucherAttachment
    {
        // Additional methods or properties can be added here
    }

    public class PaymentVoucherAttachmentMetadata
    {
      

        [Required(ErrorMessage = "Attachment Path is required")]
        [StringLength(255, ErrorMessage = "Attachment Path cannot exceed 255 characters")]
        public string AttachmentPath { get; set; }
    }
}
