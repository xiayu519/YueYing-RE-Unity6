using System;
using System.Collections.Generic;
using Jxqy.Domain.Animation;
using Jxqy.Domain.Simulation;
using Jxqy.Domain.World;
using Jxqy.Ports;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public static class JxqyCharacterStatusPresentation
    {
        public static Color ResolveColor(
            JxqyCharacter character,
            Color baseColor)
        {
            if (character == null)
                return baseColor;
            Color color = baseColor;
            if (character.HasStatus(JxqyStatusKind.Frozen) &&
                character.IsFrozenVisualEffect)
            {
                color *= new Color(
                    80f / 255f,
                    80f / 255f,
                    1f,
                    1f);
            }
            if (character.HasStatus(JxqyStatusKind.Poisoned) &&
                character.IsPoisonVisualEffect)
            {
                color *= new Color(
                    50f / 255f,
                    1f,
                    50f / 255f,
                    1f);
            }
            return color;
        }

        public static string ResolveMaterialKey(JxqyCharacter character)
        {
            return character != null &&
                   character.HasStatus(JxqyStatusKind.Petrified) &&
                   character.IsPetrifiedVisualEffect
                ? "grayscale"
                : "default";
        }

        public static bool HasSpecialDeathVisual(
            JxqyCharacter character)
        {
            return character != null && character.IsDead &&
                   (character.HasStatus(JxqyStatusKind.Frozen) &&
                    character.IsFrozenVisualEffect ||
                    character.HasStatus(JxqyStatusKind.Poisoned) &&
                    character.IsPoisonVisualEffect ||
                    character.HasStatus(JxqyStatusKind.Petrified) &&
                    character.IsPetrifiedVisualEffect);
        }
    }

    public static class JxqyWorldDepth
    {
        public const int BodyObjectBase = 1_000_000;
        public const int RowInterleavedBase = 2_000_000;
        public const int UpperMapBase = 3_000_000;
        public const int FlyingNpcBase = 4_000_000;
        public const int PlayerBase = 4_500_000;
        public const int PointerOutlineBase = 4_900_000;
    }

    public enum JxqyWorldVisualKind
    {
        Npc = 0,
        Object = 1,
        Magic = 2,
        Projectile = 3,
        BodyObject = 4,
        FlyingNpc = 5,
        CharacterEffect = 6,
        Player = 7
    }

    public sealed class JxqyWorldVisual
    {
        public string Id = string.Empty;
        public JxqyWorldVisualKind Kind;
        public int TileColumn;
        public int TileRow;
        public Vector2 WorldPosition;
        public Color Color = Color.white;
        public Color OutlineColor = Color.clear;
        public string MaterialKey = "default";
        public bool IsVisible = true;
        public JxqyAnimationPlayer Animation;
    }

    public sealed class JxqyWorldDrawCommandBuilder
    {
        private readonly int _mapColumns;

        public JxqyWorldDrawCommandBuilder(int mapColumns)
        {
            if (mapColumns <= 0)
                throw new ArgumentOutOfRangeException(nameof(mapColumns));
            _mapColumns = mapColumns;
        }

        public List<JxqyDrawCommand> Build(
            IEnumerable<JxqyWorldVisual> visuals,
            JxqyIntRect camera,
            int playerTileRow = int.MaxValue)
        {
            var result = new List<JxqyDrawCommand>();
            Build(visuals, camera, result, playerTileRow);
            return result;
        }

        public void Build(
            IEnumerable<JxqyWorldVisual> visuals,
            JxqyIntRect camera,
            List<JxqyDrawCommand> result,
            int playerTileRow = int.MaxValue)
        {
            if (visuals == null)
                throw new ArgumentNullException(nameof(visuals));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            result.Clear();
            foreach (JxqyWorldVisual visual in visuals)
            {
                if (visual == null || !visual.IsVisible ||
                    visual.Animation == null)
                    continue;
                JxqyAnimationPose pose =
                    visual.Animation.GetPose();
                if (visual.Kind == JxqyWorldVisualKind.Player)
                {
                    bool grayscale = string.Equals(
                        visual.MaterialKey,
                        "grayscale",
                        StringComparison.Ordinal);
                    AddCommand(
                        result,
                        visual,
                        pose,
                        JxqyWorldDepth.PlayerBase,
                        grayscale
                            ? "player-opaque-grayscale"
                            : "player-opaque");
                    AddCommand(
                        result,
                        visual,
                        pose,
                        JxqyWorldDepth.PlayerBase + 1,
                        grayscale
                            ? "player-occluded-grayscale"
                            : "player-occluded");
                    continue;
                }
                AddCommand(
                    result,
                    visual,
                    pose,
                    CalculateDepth(visual),
                    IsPlayerOccluder(visual, playerTileRow)
                        ? "occluder"
                        : visual.MaterialKey);
                if (visual.OutlineColor.a > 0f)
                {
                    bool wideObjectOutline =
                        visual.Kind == JxqyWorldVisualKind.Object ||
                        visual.Kind == JxqyWorldVisualKind.BodyObject;
                    AddCommand(
                        result,
                        visual,
                        pose,
                        JxqyWorldDepth.PointerOutlineBase,
                        wideObjectOutline ? "outedgewide" : "outedge",
                        visual.OutlineColor);
                }
            }
        }

        private static void AddCommand(
            ICollection<JxqyDrawCommand> result,
            JxqyWorldVisual visual,
            JxqyAnimationPose pose,
            int depth,
            string materialKey,
            Color? commandColor = null)
        {
            result.Add(new JxqyDrawCommand(
                    pose.AtlasAddress,
                    new Rect(
                        pose.AtlasX,
                        pose.AtlasY,
                        pose.Width,
                        pose.Height),
                    visual.WorldPosition,
                    new Vector2(pose.AnchorX, pose.AnchorY),
                    commandColor ?? visual.Color,
                    depth,
                    materialKey));
        }

        private static bool IsPlayerOccluder(
            JxqyWorldVisual visual,
            int playerTileRow)
        {
            if (playerTileRow == int.MaxValue ||
                !string.Equals(
                    visual.MaterialKey,
                    "default",
                    StringComparison.Ordinal))
            {
                return false;
            }
            return visual.Kind switch
            {
                JxqyWorldVisualKind.Npc =>
                    visual.TileRow > playerTileRow,
                JxqyWorldVisualKind.Magic =>
                    visual.TileRow >= playerTileRow,
                JxqyWorldVisualKind.Projectile =>
                    visual.TileRow >= playerTileRow,
                JxqyWorldVisualKind.CharacterEffect =>
                    visual.TileRow >= playerTileRow,
                _ => false
            };
        }

        private int CalculateDepth(JxqyWorldVisual visual)
        {
            int tileOrder =
                visual.TileRow * _mapColumns +
                visual.TileColumn;
            return visual.Kind switch
            {
                JxqyWorldVisualKind.BodyObject =>
                    JxqyWorldDepth.BodyObjectBase + tileOrder,
                JxqyWorldVisualKind.FlyingNpc =>
                    JxqyWorldDepth.FlyingNpcBase + tileOrder,
                JxqyWorldVisualKind.Npc =>
                    JxqyWorldDepth.RowInterleavedBase +
                    tileOrder * 10 + 1,
                JxqyWorldVisualKind.Object =>
                    JxqyWorldDepth.RowInterleavedBase +
                    tileOrder * 10 + 2,
                JxqyWorldVisualKind.Magic =>
                    JxqyWorldDepth.RowInterleavedBase +
                    tileOrder * 10 + 3,
                JxqyWorldVisualKind.Projectile =>
                    JxqyWorldDepth.RowInterleavedBase +
                    tileOrder * 10 + 3,
                JxqyWorldVisualKind.CharacterEffect =>
                    JxqyWorldDepth.RowInterleavedBase +
                    tileOrder * 10 + 4,
                _ => JxqyWorldDepth.RowInterleavedBase +
                     tileOrder * 10 + 5
            };
        }
    }
}
