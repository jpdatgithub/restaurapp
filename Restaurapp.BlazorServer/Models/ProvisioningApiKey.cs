namespace Restaurapp.BlazorServer.Models
{
    public class ProvisioningApiKey
    {
        public int Id { get; set; }
        public string KeyHash { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
    }
}
