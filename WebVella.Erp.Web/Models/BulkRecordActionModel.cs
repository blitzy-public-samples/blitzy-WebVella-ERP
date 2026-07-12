using Newtonsoft.Json;
using System;
using System.Collections.Generic;

#nullable enable

namespace WebVella.Erp.Web.Models
{
    // Request payload for the bulk delete and bulk archive endpoints. The client posts the target
    // entity name and the selected record ids. Explicit JsonProperty names give the wire contract a
    // stable, lower-case shape that matches the response envelope the controller returns.
    public class BulkRecordActionModel
    {
        [JsonProperty(PropertyName = "entityName")]
        public string? EntityName { get; set; }

        [JsonProperty(PropertyName = "recordIds")]
        public List<Guid> RecordIds { get; set; } = new();

        // Optional archive field name. The server validates this value against a trusted allowlist,
        // so a caller cannot redirect the archive write to an arbitrary field on the entity.
        [JsonProperty(PropertyName = "archiveFieldName")]
        public string ArchiveFieldName { get; set; } = "is_archived";
    }

    // Per-record outcome the controller aggregates and returns for best-effort partial-failure
    // reporting. Explicit lower-case JsonProperty names keep the item shape consistent with the
    // response envelope, and Code carries a stable, client-safe outcome code.
    public class BulkRecordActionResultItem
    {
        [JsonProperty(PropertyName = "recordId")]
        public Guid RecordId { get; set; }

        [JsonProperty(PropertyName = "success")]
        public bool Success { get; set; }

        [JsonProperty(PropertyName = "code")]
        public string Code { get; set; } = "";

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; } = "";
    }
}
