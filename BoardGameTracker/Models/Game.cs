using System.ComponentModel.DataAnnotations;

namespace BoardGameTracker.Models;

public class Game
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(255)]
    public string Owner { get; set; } = string.Empty;
    
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}