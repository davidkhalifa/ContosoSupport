using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ContosoSupport.Models
{
    public class SupportPerson
    {
        [BsonId]
        [BsonElement("alias")]
        public string Alias { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("specializations")]
        public List<string> Specializations { get; set; } = new List<string>();

        [BsonElement("current_workload")]
        public int CurrentWorkload { get; set; } = 0;

        [BsonElement("average_resolution_time")]
        public double? AverageResolutionTime { get; set; }

        [BsonElement("customer_satisfaction_rating")]
        public double? CustomerSatisfactionRating { get; set; }

        [BsonElement("seniority")]
        public string Seniority { get; set; } = string.Empty;

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;
    }

    public enum SeniorityLevel
    {
        Junior,
        MidLevel,
        Senior,
        Lead,
        Manager
    }
}