using UnityEngine;
using UnityEngine.Tilemaps;

namespace Jxqy.UnityAdapters
{
    /// <summary>
    /// Scene-baked looping MAP tile. TilemapRenderer evaluates the animation,
    /// so gameplay code does not rebuild meshes or spawn per-cell objects.
    /// </summary>
    public sealed class JxqyAnimatedTile : TileBase
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _framesPerSecond = 12f;

        public void Initialize(
            Sprite[] frames,
            float framesPerSecond)
        {
            _frames = frames;
            _framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
        }

        public override void GetTileData(
            Vector3Int position,
            ITilemap tilemap,
            ref TileData tileData)
        {
            tileData.sprite =
                _frames != null && _frames.Length > 0
                    ? _frames[0]
                    : null;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.gameObject = null;
            tileData.flags = TileFlags.None;
            tileData.colliderType = Tile.ColliderType.None;
        }

        public override bool GetTileAnimationData(
            Vector3Int position,
            ITilemap tilemap,
            ref TileAnimationData tileAnimationData)
        {
            if (_frames == null || _frames.Length <= 1)
                return false;
            tileAnimationData.animatedSprites = _frames;
            tileAnimationData.animationSpeed = _framesPerSecond;
            tileAnimationData.animationStartTime = 0f;
            return true;
        }
    }
}
