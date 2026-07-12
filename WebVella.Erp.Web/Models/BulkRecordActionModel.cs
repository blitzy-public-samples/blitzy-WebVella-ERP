using System;
using System.Collections.Generic;

namespace WebVella.Erp.Web.Models
{
    public class BulkRecordActionModel
    {
        public string EntityName { get; set; }
        public List<Guid> RecordIds { get; set; } = new();
        // Optional archive field name so the bulk-archive action can set the correct boolean field generically.
        public string ArchiveFieldName { get; set; } = "is_archived";
    }

    public class BulkRecordActionResultItem
    {
        public Guid RecordId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
