using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Content.Server.Database;

//CrystallEdge achievements
[Table("player_achievement")]
public sealed class PlayerAchievement
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, ForeignKey("Player")]
    public Guid PlayerUserId { get; set; }
    public Player Player { get; set; } = default!;

    [Required, Column("proto_id")]
    public string ProtoId { get; set; } = string.Empty;
}
//CrystallEdge achievements end
