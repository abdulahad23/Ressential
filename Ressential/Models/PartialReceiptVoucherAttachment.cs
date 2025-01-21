using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(ReceiptVoucherAttachmentMetadata))]
    public partial class ReceiptVoucherAttachment
    {
        // Additional methods or properties can be added here
    }

    public class ReceiptVoucherAttachmentMetadata
    {
        
        [Required(ErrorMessage = "Attachment Path is required")]
        [StringLength(255, ErrorMessage = "Attachment Path cannot exceed 255 characters")]
        public string AttachmentPath { get; set; }
    }
}
