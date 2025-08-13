using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ContosoSupport.Models
{
    public class SupportCase
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("Title")]
        public string? Title { get; set; }

        [BsonElement("IsComplete")]
        public bool IsComplete { get; set; }

        [BsonElement("Owner")]
        public string? Owner { get; set; }

        [BsonElement("Description")]
        public string? Description { get; set; }

        [BsonElement("AssignedSupportPerson")]
        public string? AssignedSupportPerson { get; set; }

        [BsonElement("SupportPersonAssignmentReasoning")]
        public string? SupportPersonAssignmentReasoning { get; set; }
    }
}
