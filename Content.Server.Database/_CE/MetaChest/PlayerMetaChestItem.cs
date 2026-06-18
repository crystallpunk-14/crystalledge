using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Content.Server.Database;

//CrystallEdge meta chest
[Table("player_meta_chest_item")]
public sealed class PlayerMetaChestItem
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, ForeignKey("Player")]
    public Guid PlayerUserId { get; set; }
    public Player Player { get; set; } = default!;

    /// <summary>
    /// Identifies which chest "bank" these items belong to.
    /// Different CEMetaChest entities with different ChestSlot values give players independent storages.
    /// </summary>
    [Required, Column("chest_slot")]
    public int ChestSlot { get; set; }

    /// <summary>Full YAML serialization of the item entity (including all component state).</summary>
    [Required, Column("item_yaml", TypeName = "text")]
    public string ItemYaml { get; set; } = string.Empty;

    [Column("grid_x")]
    public int GridX { get; set; }

    [Column("grid_y")]
    public int GridY { get; set; }

    /// <summary>Direction enum value cast to byte.</summary>
    [Column("grid_rotation")]
    public byte GridRotation { get; set; }
}
//CrystallEdge meta chest end
