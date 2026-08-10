using System;
using System.Collections.Generic;
using System.Linq;
using Jxqy.Domain.World;

namespace Jxqy.Domain.Simulation
{
    public interface IJxqyTileCollisionMap
    {
        int Columns { get; }
        int Rows { get; }
        bool IsObstacle(JxqyIntPoint tile);
        bool IsObstacleForCharacter(JxqyIntPoint tile);
        bool IsObstacleForCharacterJump(JxqyIntPoint tile);
        bool IsObstacleForMagic(JxqyIntPoint tile);
        int GetTrapIndex(JxqyIntPoint tile);
    }

    public sealed class JxqyRuntimeCollisionMap : IJxqyTileCollisionMap
    {
        private readonly JxqyRuntimeMapData _map;
        private readonly JxqyObjectManager _objects;
        private readonly JxqyNpcManager _npcs;

        public JxqyRuntimeCollisionMap(
            JxqyRuntimeMapData map,
            JxqyObjectManager objects = null,
            JxqyNpcManager npcs = null)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _objects = objects;
            _npcs = npcs;
        }

        public int Columns => _map.Columns;
        public int Rows => _map.Rows;

        public bool IsObstacle(JxqyIntPoint tile)
        {
            return _map.IsObstacle(tile.X, tile.Y);
        }

        public bool IsObstacleForCharacter(JxqyIntPoint tile)
        {
            return _map.IsObstacleForCharacter(tile.X, tile.Y) ||
                   _objects?.IsObstacle(tile) == true ||
                   IsNpcObstacle(tile);
        }

        public bool IsObstacleForCharacterJump(JxqyIntPoint tile)
        {
            return _map.IsObstacleForCharacterJump(tile.X, tile.Y) ||
                   _objects?.IsObstacle(tile) == true ||
                   IsNpcObstacle(tile);
        }

        public bool IsObstacleForMagic(JxqyIntPoint tile)
        {
            return _map.IsObstacleForMagic(tile.X, tile.Y);
        }

        public int GetTrapIndex(JxqyIntPoint tile)
        {
            return _map.GetTrapIndex(tile.X, tile.Y);
        }

        private bool IsNpcObstacle(JxqyIntPoint tile)
        {
            if (_npcs == null)
                return false;
            foreach (JxqyNpc npc in _npcs.Npcs)
            {
                if (npc.Life > 0 &&
                    npc.Kind != JxqyCharacterKind.Flyer &&
                    npc.TilePosition.Equals(tile))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public sealed class JxqyCollisionGrid : IJxqyTileCollisionMap
    {
        private readonly bool[] _solid;
        private readonly bool[] _characterObstacle;
        private readonly int[] _trapIndices;

        public JxqyCollisionGrid(int columns, int rows)
        {
            if (columns <= 0)
                throw new ArgumentOutOfRangeException(nameof(columns));
            if (rows <= 0)
                throw new ArgumentOutOfRangeException(nameof(rows));
            Columns = columns;
            Rows = rows;
            _solid = new bool[checked(columns * rows)];
            _characterObstacle = new bool[_solid.Length];
            _trapIndices = new int[_solid.Length];
        }

        public int Columns { get; }
        public int Rows { get; }

        public void SetObstacle(
            JxqyIntPoint tile,
            bool blocked,
            bool solid = true)
        {
            int index = GetIndex(tile);
            _characterObstacle[index] = blocked;
            _solid[index] = blocked && solid;
        }

        public void SetTrapIndex(JxqyIntPoint tile, int trapIndex)
        {
            if (trapIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(trapIndex));
            _trapIndices[GetIndex(tile)] = trapIndex;
        }

        public bool IsObstacle(JxqyIntPoint tile)
        {
            return !IsInside(tile) || _solid[GetIndex(tile)];
        }

        public bool IsObstacleForCharacter(JxqyIntPoint tile)
        {
            return !IsInside(tile) ||
                   _characterObstacle[GetIndex(tile)];
        }

        public bool IsObstacleForCharacterJump(JxqyIntPoint tile)
        {
            return IsObstacleForCharacter(tile);
        }

        public bool IsObstacleForMagic(JxqyIntPoint tile)
        {
            return IsObstacle(tile);
        }

        public int GetTrapIndex(JxqyIntPoint tile)
        {
            return IsInside(tile) ? _trapIndices[GetIndex(tile)] : 0;
        }

        private bool IsInside(JxqyIntPoint tile)
        {
            return tile.X >= 0 && tile.X < Columns &&
                   tile.Y >= 0 && tile.Y < Rows;
        }

        private int GetIndex(JxqyIntPoint tile)
        {
            if (!IsInside(tile))
                throw new ArgumentOutOfRangeException(nameof(tile));
            return tile.X + tile.Y * Columns;
        }
    }

    public enum JxqyPathType
    {
        PathOneStep,
        SimpleMaxNpcTry,
        PerfectMaxNpcTry,
        PerfectMaxPlayerTry,
        PathStraightLine,
    }

    public static class JxqyPathfinder
    {
        public static IReadOnlyList<JxqyIntPoint> GetAllNeighbors(
            JxqyIntPoint tile)
        {
            var neighbors = new JxqyIntPoint[8];
            FillNeighbors(tile, neighbors);
            return neighbors;
        }

        private static void FillNeighbors(
            JxqyIntPoint tile,
            JxqyIntPoint[] neighbors)
        {
            int x = tile.X;
            int y = tile.Y;
            if (y % 2 == 0)
            {
                neighbors[0] = new JxqyIntPoint(x, y + 2);
                neighbors[1] = new JxqyIntPoint(x - 1, y + 1);
                neighbors[2] = new JxqyIntPoint(x - 1, y);
                neighbors[3] = new JxqyIntPoint(x - 1, y - 1);
                neighbors[4] = new JxqyIntPoint(x, y - 2);
                neighbors[5] = new JxqyIntPoint(x, y - 1);
                neighbors[6] = new JxqyIntPoint(x + 1, y);
                neighbors[7] = new JxqyIntPoint(x, y + 1);
                return;
            }
            neighbors[0] = new JxqyIntPoint(x, y + 2);
            neighbors[1] = new JxqyIntPoint(x, y + 1);
            neighbors[2] = new JxqyIntPoint(x - 1, y);
            neighbors[3] = new JxqyIntPoint(x, y - 1);
            neighbors[4] = new JxqyIntPoint(x, y - 2);
            neighbors[5] = new JxqyIntPoint(x + 1, y - 1);
            neighbors[6] = new JxqyIntPoint(x + 1, y);
            neighbors[7] = new JxqyIntPoint(x + 1, y + 1);
        }

        public static bool CanMoveInDirection(
            int direction,
            int directionCount)
        {
            switch (directionCount)
            {
                case 1:
                    return direction == 0;
                case 2:
                    return direction == 0 || direction == 4;
                case 4:
                    return direction == 0 || direction == 2 ||
                           direction == 4 || direction == 6;
                default:
                    return direction >= 0 && direction < directionCount;
            }
        }

        public static IReadOnlyList<JxqyFloat2> FindLegacyPath(
            IJxqyTileCollisionMap map,
            JxqyIntPoint start,
            JxqyIntPoint end,
            JxqyPathType pathType,
            Func<JxqyIntPoint, bool> hasDynamicObstacle = null,
            int directionCount = 8,
            bool disableMaximumExpandedNodes = false)
        {
            switch (pathType)
            {
                case JxqyPathType.PathOneStep:
                    return FindStepPath(
                        map,
                        start,
                        end,
                        10,
                        hasDynamicObstacle,
                        directionCount);
                case JxqyPathType.SimpleMaxNpcTry:
                    return FindSimplePath(
                        map,
                        start,
                        end,
                        100,
                        hasDynamicObstacle,
                        directionCount);
                case JxqyPathType.PerfectMaxNpcTry:
                    return FindPath(
                        map,
                        start,
                        end,
                        hasDynamicObstacle,
                        directionCount,
                        100);
                case JxqyPathType.PerfectMaxPlayerTry:
                    return FindPath(
                        map,
                        start,
                        end,
                        hasDynamicObstacle,
                        directionCount,
                        disableMaximumExpandedNodes ? -1 : 500);
                case JxqyPathType.PathStraightLine:
                    return FindStraightLinePath(start, end, 100);
                default:
                    throw new ArgumentOutOfRangeException(nameof(pathType));
            }
        }

        private static IReadOnlyList<JxqyFloat2> FindStepPath(
            IJxqyTileCollisionMap map,
            JxqyIntPoint start,
            JxqyIntPoint end,
            int maximumSteps,
            Func<JxqyIntPoint, bool> hasDynamicObstacle,
            int directionCount)
        {
            ValidatePathArguments(map, directionCount);
            if (start.Equals(end) || map.IsObstacleForCharacter(end))
                return Array.Empty<JxqyFloat2>();

            var tiles = new List<JxqyIntPoint> { start };
            var visited = new HashSet<JxqyIntPoint> { start };
            JxqyIntPoint current = start;
            var neighbors = new JxqyIntPoint[8];
            for (int attempt = 0; attempt < 100; attempt++)
            {
                FillNeighbors(current, neighbors);
                int preferredDirection = JxqyDirection.GetIndex(
                    ToWorldVector(current, end),
                    8);
                int blockedDirections =
                    GetBlockedDirectionsMask(map, neighbors);
                int selectedDirection = -1;
                for (int offset = 0; offset < 8; offset++)
                {
                    int direction = GetLegacyDirectionCandidate(
                        preferredDirection,
                        offset);
                    JxqyIntPoint candidate = neighbors[direction];
                    if (!CanMoveInDirection(direction, directionCount) ||
                        (blockedDirections & (1 << direction)) != 0 ||
                        map.IsObstacleForCharacter(candidate) ||
                        hasDynamicObstacle?.Invoke(candidate) == true ||
                        visited.Contains(candidate))
                    {
                        continue;
                    }
                    selectedDirection = direction;
                    break;
                }
                if (selectedDirection < 0)
                    break;

                current = neighbors[selectedDirection];
                tiles.Add(current);
                visited.Add(current);
                if (tiles.Count > maximumSteps || current.Equals(end))
                    break;
            }
            return tiles.Count < 2
                ? Array.Empty<JxqyFloat2>()
                : BuildWorldPath(tiles);
        }

        private static IReadOnlyList<JxqyFloat2> FindSimplePath(
            IJxqyTileCollisionMap map,
            JxqyIntPoint start,
            JxqyIntPoint end,
            int maximumExpandedNodes,
            Func<JxqyIntPoint, bool> hasDynamicObstacle,
            int directionCount)
        {
            ValidatePathArguments(map, directionCount);
            if (start.Equals(end) || map.IsObstacleForCharacter(end))
                return Array.Empty<JxqyFloat2>();

            var cameFrom = new Dictionary<JxqyIntPoint, JxqyIntPoint>();
            var frontier = new MinHeap();
            frontier.Add(start, 0f);
            int expanded = 0;
            bool reached = false;
            var neighbors = new JxqyIntPoint[8];
            while (frontier.Count > 0)
            {
                if (expanded++ > maximumExpandedNodes)
                    break;
                JxqyIntPoint current = frontier.RemoveMinimum();
                if (current.Equals(end))
                {
                    reached = true;
                    break;
                }
                if (!current.Equals(start) &&
                    hasDynamicObstacle?.Invoke(current) == true)
                {
                    continue;
                }

                FillNeighbors(current, neighbors);
                int blockedDirections =
                    GetBlockedDirectionsMask(map, neighbors);
                for (int direction = 0; direction < neighbors.Length;
                     direction++)
                {
                    if (!CanMoveInDirection(direction, directionCount) ||
                        (blockedDirections & (1 << direction)) != 0)
                    {
                        continue;
                    }
                    JxqyIntPoint neighbor = neighbors[direction];
                    if (cameFrom.ContainsKey(neighbor))
                        continue;
                    frontier.Add(neighbor, GetWorldDistance(neighbor, end));
                    cameFrom[neighbor] = current;
                }
            }
            return reached
                ? BuildWorldPath(cameFrom, start, end)
                : Array.Empty<JxqyFloat2>();
        }

        private static IReadOnlyList<JxqyFloat2> FindStraightLinePath(
            JxqyIntPoint start,
            JxqyIntPoint end,
            int maximumSteps)
        {
            if (start.Equals(end))
                return Array.Empty<JxqyFloat2>();
            var tiles = new List<JxqyIntPoint> { start };
            JxqyIntPoint current = start;
            for (int step = 0;
                 step < maximumSteps && !current.Equals(end);
                 step++)
            {
                IReadOnlyList<JxqyIntPoint> neighbors =
                    GetAllNeighbors(current);
                current = neighbors
                    .OrderBy(candidate => GetWorldDistance(candidate, end))
                    .First();
                tiles.Add(current);
            }
            return BuildWorldPath(tiles);
        }

        private static int GetLegacyDirectionCandidate(
            int preferredDirection,
            int candidateIndex)
        {
            if (candidateIndex == 0)
                return preferredDirection;
            int distance = (candidateIndex + 1) / 2;
            int sign = candidateIndex % 2 == 1 ? 1 : -1;
            return (preferredDirection + sign * distance + 8) % 8;
        }

        private static JxqyFloat2 ToWorldVector(
            JxqyIntPoint from,
            JxqyIntPoint to)
        {
            JxqyIntPoint fromWorld =
                JxqyIsometricMapMath.TileToWorldPixel(from.X, from.Y);
            JxqyIntPoint toWorld =
                JxqyIsometricMapMath.TileToWorldPixel(to.X, to.Y);
            return new JxqyFloat2(
                toWorld.X - fromWorld.X,
                toWorld.Y - fromWorld.Y);
        }

        private static void ValidatePathArguments(
            IJxqyTileCollisionMap map,
            int directionCount)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (directionCount != 1 && directionCount != 2 &&
                directionCount != 4 && directionCount != 8)
            {
                throw new ArgumentOutOfRangeException(nameof(directionCount));
            }
        }

        public static int GetViewTileDistance(
            JxqyIntPoint start,
            JxqyIntPoint end)
        {
            int startX = start.X;
            int startY = start.Y;
            if (end.Y % 2 != startY % 2)
            {
                startY += end.Y < startY ? 1 : -1;
                if (end.Y % 2 == 0)
                    startX += end.X > startX ? 1 : 0;
                else
                    startX += end.X < startX ? -1 : 0;
            }
            return Math.Abs(startX - end.X) +
                   Math.Abs(startY - end.Y) / 2;
        }

        public static bool CanViewTarget(
            IJxqyTileCollisionMap map,
            JxqyIntPoint start,
            JxqyIntPoint end,
            int visionRadius)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            const int maximumLegacyVisionRadius = 80;
            if (visionRadius > maximumLegacyVisionRadius)
                return false;
            if (start.Equals(end))
                return true;
            if (map.IsObstacleForMagic(end))
                return false;

            var frontier = new MinHeap();
            frontier.Add(start, 0f);
            while (frontier.Count > 0)
            {
                JxqyIntPoint current = frontier.RemoveMinimum();
                if (current.Equals(end))
                    return true;
                if (map.IsObstacle(current) || visionRadius < 0)
                    return false;

                foreach (JxqyIntPoint neighbor in GetAllNeighbors(current))
                {
                    frontier.Add(
                        neighbor,
                        GetViewTileDistance(neighbor, end));
                }
                visionRadius--;
            }
            return false;
        }

        public static IReadOnlyList<JxqyFloat2> FindPath(
            IJxqyTileCollisionMap map,
            JxqyIntPoint start,
            JxqyIntPoint end,
            Func<JxqyIntPoint, bool> hasDynamicObstacle = null,
            int directionCount = 8,
            int maximumExpandedNodes = 500)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (start.Equals(end) || map.IsObstacleForCharacter(end))
                return Array.Empty<JxqyFloat2>();
            if (directionCount != 1 && directionCount != 2 &&
                directionCount != 4 && directionCount != 8)
                throw new ArgumentOutOfRangeException(nameof(directionCount));

            var frontier = new MinHeap();
            var cameFrom = new Dictionary<JxqyIntPoint, JxqyIntPoint>();
            var cost = new Dictionary<JxqyIntPoint, float>
            {
                [start] = 0f,
            };
            frontier.Add(start, 0f);
            int expanded = 0;
            bool reached = false;
            var neighbors = new JxqyIntPoint[8];

            while (frontier.Count > 0)
            {
                JxqyIntPoint current = frontier.RemoveMinimum();
                if (current.Equals(end))
                {
                    reached = true;
                    break;
                }
                if (maximumExpandedNodes >= 0 &&
                    expanded++ >= maximumExpandedNodes)
                    break;

                FillNeighbors(current, neighbors);
                int blockedDirections =
                    GetBlockedDirectionsMask(map, neighbors);
                for (int direction = 0;
                     direction < neighbors.Length;
                     direction++)
                {
                    if (!CanMoveInDirection(direction, directionCount) ||
                        (blockedDirections & (1 << direction)) != 0)
                        continue;
                    JxqyIntPoint next = neighbors[direction];
                    if (map.IsObstacleForCharacter(next))
                        continue;
                    if (!next.Equals(end) &&
                        hasDynamicObstacle != null &&
                        hasDynamicObstacle(next))
                        continue;

                    float nextCost = cost[current] +
                                     GetWorldDistance(current, next);
                    if (cost.TryGetValue(next, out float known) &&
                        known <= nextCost)
                        continue;
                    cost[next] = nextCost;
                    float priority =
                        nextCost + GetWorldDistance(next, end);
                    frontier.Add(next, priority);
                    cameFrom[next] = current;
                }
            }

            if (!reached)
                return Array.Empty<JxqyFloat2>();
            var reversed = new List<JxqyIntPoint> { end };
            JxqyIntPoint step = end;
            while (!step.Equals(start))
            {
                if (!cameFrom.TryGetValue(step, out step))
                    return Array.Empty<JxqyFloat2>();
                reversed.Add(step);
            }
            reversed.Reverse();
            var result = new List<JxqyFloat2>(reversed.Count);
            foreach (JxqyIntPoint tile in reversed)
            {
                JxqyIntPoint world =
                    JxqyIsometricMapMath.TileToWorldPixel(tile.X, tile.Y);
                result.Add(new JxqyFloat2(world.X, world.Y));
            }
            return result;
        }

        public static IReadOnlyList<JxqyFloat2>
            FindPathToNearestReachable(
                IJxqyTileCollisionMap map,
                JxqyIntPoint start,
                JxqyIntPoint requestedEnd,
                out JxqyIntPoint resolvedEnd,
                Func<JxqyIntPoint, bool> hasDynamicObstacle = null,
                int directionCount = 8,
                int maximumExpandedNodes = 500)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (directionCount != 1 && directionCount != 2 &&
                directionCount != 4 && directionCount != 8)
                throw new ArgumentOutOfRangeException(nameof(directionCount));

            resolvedEnd = start;
            if (start.Equals(requestedEnd))
                return Array.Empty<JxqyFloat2>();

            var frontier = new MinHeap();
            var cameFrom = new Dictionary<JxqyIntPoint, JxqyIntPoint>();
            var cost = new Dictionary<JxqyIntPoint, float>
            {
                [start] = 0f,
            };
            frontier.Add(start, GetWorldDistance(start, requestedEnd));
            float bestDistance = GetWorldDistance(start, requestedEnd);
            float bestCost = 0f;
            int expanded = 0;
            var neighbors = new JxqyIntPoint[8];

            while (frontier.Count > 0)
            {
                JxqyIntPoint current = frontier.RemoveMinimum();
                float distance = GetWorldDistance(current, requestedEnd);
                float currentCost = cost[current];
                if (distance < bestDistance ||
                    distance.Equals(bestDistance) &&
                    currentCost < bestCost)
                {
                    resolvedEnd = current;
                    bestDistance = distance;
                    bestCost = currentCost;
                }
                if (current.Equals(requestedEnd) &&
                    !map.IsObstacleForCharacter(current))
                {
                    resolvedEnd = current;
                    break;
                }
                if (maximumExpandedNodes >= 0 &&
                    expanded++ >= maximumExpandedNodes)
                    break;

                FillNeighbors(current, neighbors);
                int blockedDirections =
                    GetBlockedDirectionsMask(map, neighbors);
                for (int direction = 0;
                     direction < neighbors.Length;
                     direction++)
                {
                    if (!CanMoveInDirection(direction, directionCount) ||
                        (blockedDirections & (1 << direction)) != 0)
                        continue;
                    JxqyIntPoint next = neighbors[direction];
                    if (map.IsObstacleForCharacter(next) ||
                        hasDynamicObstacle != null &&
                        hasDynamicObstacle(next))
                    {
                        continue;
                    }

                    float nextCost = currentCost +
                                     GetWorldDistance(current, next);
                    if (cost.TryGetValue(next, out float known) &&
                        known <= nextCost)
                    {
                        continue;
                    }
                    cost[next] = nextCost;
                    frontier.Add(
                        next,
                        nextCost +
                        GetWorldDistance(next, requestedEnd));
                    cameFrom[next] = current;
                }
            }

            if (resolvedEnd.Equals(start))
                return Array.Empty<JxqyFloat2>();
            return BuildWorldPath(cameFrom, start, resolvedEnd);
        }

        private static IReadOnlyList<JxqyFloat2> BuildWorldPath(
            IReadOnlyDictionary<JxqyIntPoint, JxqyIntPoint> cameFrom,
            JxqyIntPoint start,
            JxqyIntPoint end)
        {
            var reversed = new List<JxqyIntPoint> { end };
            JxqyIntPoint step = end;
            while (!step.Equals(start))
            {
                if (!cameFrom.TryGetValue(step, out step))
                    return Array.Empty<JxqyFloat2>();
                reversed.Add(step);
            }
            reversed.Reverse();
            return BuildWorldPath(reversed);
        }

        private static IReadOnlyList<JxqyFloat2> BuildWorldPath(
            IReadOnlyList<JxqyIntPoint> tiles)
        {
            var result = new List<JxqyFloat2>(tiles.Count);
            foreach (JxqyIntPoint tile in tiles)
            {
                JxqyIntPoint world =
                    JxqyIsometricMapMath.TileToWorldPixel(tile.X, tile.Y);
                result.Add(new JxqyFloat2(world.X, world.Y));
            }
            return result;
        }

        private static int GetBlockedDirectionsMask(
            IJxqyTileCollisionMap map,
            JxqyIntPoint[] neighbors)
        {
            int blocked = 0;
            for (int index = 1; index < neighbors.Length; index += 2)
            {
                if (!map.IsObstacleForCharacter(neighbors[index]))
                    continue;
                blocked |= 1 << index;
                if (!map.IsObstacle(neighbors[index]))
                    continue;
                switch (index)
                {
                    case 1:
                        blocked |= 1 << 0;
                        blocked |= 1 << 2;
                        break;
                    case 3:
                        blocked |= 1 << 2;
                        blocked |= 1 << 4;
                        break;
                    case 5:
                        blocked |= 1 << 4;
                        blocked |= 1 << 6;
                        break;
                    case 7:
                        blocked |= 1 << 0;
                        blocked |= 1 << 6;
                        break;
                }
            }
            return blocked;
        }

        private static float GetWorldDistance(
            JxqyIntPoint left,
            JxqyIntPoint right)
        {
            JxqyIntPoint leftWorld =
                JxqyIsometricMapMath.TileToWorldPixel(left.X, left.Y);
            JxqyIntPoint rightWorld =
                JxqyIsometricMapMath.TileToWorldPixel(right.X, right.Y);
            long x = leftWorld.X - rightWorld.X;
            long y = leftWorld.Y - rightWorld.Y;
            return (float)Math.Sqrt(x * x + y * y);
        }

        private sealed class MinHeap
        {
            private readonly List<Node> _nodes = new List<Node>();
            private long _sequence;

            public int Count => _nodes.Count;

            public void Add(JxqyIntPoint tile, float priority)
            {
                var node = new Node(tile, priority, _sequence++);
                _nodes.Add(node);
                int index = _nodes.Count - 1;
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (!_nodes[index].IsBefore(_nodes[parent]))
                        break;
                    Node swap = _nodes[index];
                    _nodes[index] = _nodes[parent];
                    _nodes[parent] = swap;
                    index = parent;
                }
            }

            public JxqyIntPoint RemoveMinimum()
            {
                Node minimum = _nodes[0];
                int last = _nodes.Count - 1;
                _nodes[0] = _nodes[last];
                _nodes.RemoveAt(last);
                int index = 0;
                while (index < _nodes.Count)
                {
                    int left = index * 2 + 1;
                    int right = left + 1;
                    if (left >= _nodes.Count)
                        break;
                    int child = right < _nodes.Count &&
                                _nodes[right].IsBefore(_nodes[left])
                        ? right
                        : left;
                    if (!_nodes[child].IsBefore(_nodes[index]))
                        break;
                    Node swap = _nodes[index];
                    _nodes[index] = _nodes[child];
                    _nodes[child] = swap;
                    index = child;
                }
                return minimum.Tile;
            }

            private readonly struct Node
            {
                public Node(
                    JxqyIntPoint tile,
                    float priority,
                    long sequence)
                {
                    Tile = tile;
                    Priority = priority;
                    Sequence = sequence;
                }

                public JxqyIntPoint Tile { get; }
                private float Priority { get; }
                private long Sequence { get; }

                public bool IsBefore(Node other)
                {
                    return Priority < other.Priority ||
                           Priority.Equals(other.Priority) &&
                           Sequence < other.Sequence;
                }
            }
        }
    }

    public enum JxqyObjectKind
    {
        Dynamic,
        Static,
        Body,
        LoopingSound,
        RandomSound,
        Door,
        Trap,
        Drop,
    }

    public sealed class JxqyWorldObject : JxqySprite
    {
        public JxqyWorldObject()
            : base(0)
        {
        }

        public string Name { get; set; } = string.Empty;
        public string ResourceFileName { get; set; } = string.Empty;
        public string WavFileName { get; set; } = string.Empty;
        public JxqyObjectKind Kind { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public int Height { get; set; }
        public int Frame { get; set; }
        public int Damage { get; set; }
        public int LightRadius { get; set; }
        public string ScriptAddress { get; set; } = string.Empty;
        public string RightScriptAddress { get; set; } = string.Empty;
        public string TimerScriptAddress { get; set; } = string.Empty;
        public int TimerScriptIntervalMilliseconds { get; set; } = 1000;
        public string ReviveNpcFileName { get; set; } = string.Empty;
        public float MillisecondsToRemove { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsOpen { get; set; }
        public bool IsRemoved { get; set; }
        public bool IsObstacle =>
            Kind == JxqyObjectKind.Dynamic ||
            Kind == JxqyObjectKind.Static ||
            Kind == JxqyObjectKind.Door;
        public bool IsTrap => Kind == JxqyObjectKind.Trap;
        public bool IsDrop => Kind == JxqyObjectKind.Drop;
        public bool IsInteractive =>
            !string.IsNullOrEmpty(ScriptAddress) ||
            !string.IsNullOrEmpty(RightScriptAddress);

        public bool TickLifetime(float elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0 ||
                float.IsNaN(elapsedMilliseconds) ||
                float.IsInfinity(elapsedMilliseconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedMilliseconds));
            }
            if (IsRemoved || MillisecondsToRemove <= 0)
                return false;
            MillisecondsToRemove = Math.Max(
                0,
                MillisecondsToRemove - elapsedMilliseconds);
            if (MillisecondsToRemove > 0)
                return false;
            IsRemoved = true;
            return true;
        }
    }

    public sealed class JxqyObjectManager
    {
        private readonly List<JxqyWorldObject> _objects =
            new List<JxqyWorldObject>();

        public IReadOnlyList<JxqyWorldObject> Objects => _objects;

        public void Add(JxqyWorldObject worldObject)
        {
            if (worldObject == null)
                throw new ArgumentNullException(nameof(worldObject));
            _objects.Add(worldObject);
        }

        public void Clear()
        {
            _objects.Clear();
        }

        public bool Remove(string name)
        {
            JxqyWorldObject found = Find(name);
            return found != null && _objects.Remove(found);
        }

        public bool Remove(JxqyWorldObject worldObject)
        {
            return worldObject != null && _objects.Remove(worldObject);
        }

        public JxqyWorldObject Find(string name)
        {
            return _objects.Find(item =>
                string.Equals(
                    item.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<JxqyWorldObject> At(JxqyIntPoint tile)
        {
            return _objects.FindAll(item =>
                !item.IsRemoved && item.TilePosition.Equals(tile));
        }

        public bool IsObstacle(JxqyIntPoint tile)
        {
            return _objects.Exists(item =>
                !item.IsRemoved && item.IsObstacle &&
                item.TilePosition.Equals(tile));
        }
    }

    public sealed class JxqyRevivedNpcRequest
    {
        public string NpcFileName { get; set; } = string.Empty;
        public JxqyIntPoint TilePosition { get; set; }
        public int Direction { get; set; }
        public JxqyRelationType Relation { get; set; }
        public float LifeMilliseconds { get; set; }
    }

    public sealed class JxqyBodyRevivalResult
    {
        public List<JxqyWorldObject> RemovedBodies { get; } =
            new List<JxqyWorldObject>();
        public List<JxqyRevivedNpcRequest> RevivedNpcs { get; } =
            new List<JxqyRevivedNpcRequest>();
    }

    public static class JxqyBodyRevivalSystem
    {
        public static JxqyBodyRevivalResult Resolve(
            JxqyObjectManager objects,
            JxqyCharacter source,
            JxqyMagicDefinition magic,
            JxqyFloat2 destination)
        {
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (magic == null)
                throw new ArgumentNullException(nameof(magic));

            var result = new JxqyBodyRevivalResult();
            if (magic.ReviveBodyRadius <= 0)
                return result;

            JxqyIntPoint destinationTile =
                JxqyIsometricMapMath.WorldPixelToTile(
                    (int)Math.Round(destination.X),
                    (int)Math.Round(destination.Y));
            JxqyWorldObject[] bodies = objects.Objects
                .Where(item =>
                    !item.IsRemoved &&
                    item.Kind == JxqyObjectKind.Body &&
                    JxqyPathfinder.GetViewTileDistance(
                        destinationTile,
                        item.TilePosition) <= magic.ReviveBodyRadius)
                .ToArray();

            foreach (JxqyWorldObject body in bodies)
            {
                body.IsRemoved = true;
                objects.Remove(body);
                result.RemovedBodies.Add(body);
            }

            JxqyRelationType relation =
                source is JxqyPlayer ||
                source.Relation == JxqyRelationType.Friend
                    ? JxqyRelationType.Friend
                    : JxqyRelationType.Enemy;
            int maximum = Math.Max(0, magic.ReviveBodyMaxCount);
            foreach (JxqyWorldObject body in bodies)
            {
                if (!string.IsNullOrWhiteSpace(
                        body.ReviveNpcFileName) &&
                    (maximum == 0 || result.RevivedNpcs.Count < maximum))
                {
                    result.RevivedNpcs.Add(new JxqyRevivedNpcRequest
                    {
                        NpcFileName = body.ReviveNpcFileName,
                        TilePosition = body.TilePosition,
                        Direction = body.CurrentDirection,
                        Relation = relation,
                        LifeMilliseconds = Math.Max(
                            0,
                            magic.ReviveBodyLifeMilliseconds),
                    });
                }
                if (maximum > 0 && result.RevivedNpcs.Count >= maximum)
                    break;
            }
            return result;
        }
    }

    public enum JxqyNpcIntent
    {
        Idle,
        Move,
        Attack,
        Flee,
    }

    public sealed class JxqyNpc : JxqyCharacter
    {
        private readonly List<JxqyIntPoint> _fixedPath =
            new List<JxqyIntPoint>();
        private readonly List<JxqyIntPoint> _ambientPath =
            new List<JxqyIntPoint>();
        private string _fixedPositionData = string.Empty;
        internal float AiRepathCooldownSeconds { get; set; }
        internal JxqyIntPoint AiPlannedNextTile { get; set; }
        internal bool HasAiPlannedNextTile { get; set; }
        internal JxqyIntPoint AmbientDestination { get; set; }
        internal bool HasAmbientDestination { get; set; }
        public string NpcIniFileName { get; set; } = string.Empty;
        public int Action { get; set; }
        public int PathFinderMode { get; set; }
        public string FixedPositionData
        {
            get => _fixedPositionData;
            set
            {
                _fixedPositionData = value ?? string.Empty;
                _fixedPath.Clear();
                _fixedPath.AddRange(ParseFixedPath(_fixedPositionData));
                if (_fixedPath.Count == 0)
                    CurrentFixedPositionIndex = 0;
                else if (CurrentFixedPositionIndex >= _fixedPath.Count)
                    CurrentFixedPositionIndex = _fixedPath.Count - 1;
            }
        }
        public int CurrentFixedPositionIndex { get; set; }
        public IReadOnlyList<JxqyIntPoint> FixedPath => _fixedPath;
        internal IList<JxqyIntPoint> AmbientPath => _ambientPath;
        public int Group { get; set; }
        public int VisionRadius { get; set; } = 9;
        public int AttackRadius { get; set; } = 1;
        public int IdleFrames { get; set; }
        public int LightRadius { get; set; }
        public float AttackIntervalSeconds =>
            Math.Max(0, IdleFrames) / 60f;
        public bool NoAutoAttackPlayer { get; set; }
        public bool StopFindingTarget { get; set; }
        public string ResourceFileName { get; set; } = string.Empty;
        public int ActionType { get; set; }
        public float BlindMilliseconds { get; set; }
        public int DestinationMapPosX { get; set; }
        public int DestinationMapPosY { get; set; }
        public int KeepAttackX { get; set; }
        public int KeepAttackY { get; set; }
        public int CanEquip { get; set; }
        public int CanLevelUp { get; set; }
        public string BodyFileName { get; set; } = string.Empty;
        public bool IsBodyCreated { get; set; }
        public bool IsMagicSummon { get; set; }
        public string EquipmentBackgroundFileName { get; set; } = string.Empty;
        public JxqyEquipmentManager Equipment { get; } =
            new JxqyEquipmentManager();
        public Dictionary<JxqyEquipmentSlot, string> EquipmentFileNames
        {
            get;
        } = new Dictionary<JxqyEquipmentSlot, string>();
        public JxqyCharacter FollowTarget { get; internal set; }
        public JxqyNpcIntent Intent { get; internal set; }
        public bool IsFollowingScriptTarget { get; private set; }
        public JxqyPathType PathType
        {
            get
            {
                if (Kind == JxqyCharacterKind.Flyer)
                    return JxqyPathType.PathStraightLine;
                if (PathFinderMode == 1 ||
                    Kind == JxqyCharacterKind.Follower)
                {
                    return JxqyPathType.PerfectMaxNpcTry;
                }
                if (Kind == JxqyCharacterKind.Normal ||
                    Kind == JxqyCharacterKind.Eventer)
                {
                    return JxqyPathType.PerfectMaxPlayerTry;
                }
                if (PathFinderMode == 0 || Action == 2 ||
                    Kind == JxqyCharacterKind.Fighter &&
                    Relation == JxqyRelationType.Enemy)
                {
                    return JxqyPathType.PathOneStep;
                }
                return JxqyPathType.PerfectMaxNpcTry;
            }
        }

        public static IReadOnlyList<JxqyIntPoint> ParseFixedPath(
            string fixedPositionData)
        {
            if (string.IsNullOrEmpty(fixedPositionData) ||
                fixedPositionData.Length < 32)
            {
                return Array.Empty<JxqyIntPoint>();
            }
            int chunkCount = fixedPositionData.Length / 8;
            if (chunkCount % 2 != 0)
                chunkCount--;
            var path = new List<JxqyIntPoint>();
            try
            {
                for (int index = 0; index < chunkCount; index += 2)
                {
                    int x = int.Parse(
                        fixedPositionData.Substring(index * 8, 2),
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture);
                    int y = int.Parse(
                        fixedPositionData.Substring((index + 1) * 8, 2),
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture);
                    if (x == 0 && y == 0)
                        break;
                    path.Add(new JxqyIntPoint(x, y));
                }
            }
            catch (FormatException)
            {
                return Array.Empty<JxqyIntPoint>();
            }
            catch (ArgumentOutOfRangeException)
            {
                return Array.Empty<JxqyIntPoint>();
            }
            catch (OverflowException)
            {
                return Array.Empty<JxqyIntPoint>();
            }
            return path;
        }

        public void Follow(JxqyCharacter target)
        {
            FollowTarget = target;
            IsFollowingScriptTarget = target != null;
            Intent = target == null
                ? JxqyNpcIntent.Idle
                : JxqyNpcIntent.Move;
        }
    }

    public sealed class JxqyNpcManager
    {
        private const float FailedPathRetrySeconds = 0.25f;
        private readonly List<JxqyNpc> _npcs = new List<JxqyNpc>();
        private readonly JxqyPlayer _player;
        private readonly JxqyObjectManager _objects;
        private readonly IJxqyTileCollisionMap _map;
        private readonly JxqyDeterministicRandom _random;

        public JxqyNpcManager(
            JxqyPlayer player,
            JxqyObjectManager objects,
            IJxqyTileCollisionMap map,
            JxqyDeterministicRandom random = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _random = random ?? new JxqyDeterministicRandom(1);
        }

        public bool IsAiDisabled { get; set; }
        public IReadOnlyList<JxqyNpc> Npcs => _npcs;
        public int PathPlansLastTick { get; private set; }
        public long PathPlansTotal { get; private set; }

        public void Add(JxqyNpc npc)
        {
            if (npc == null)
                throw new ArgumentNullException(nameof(npc));
            _npcs.Add(npc);
        }

        public void Clear(bool keepFollowers = false)
        {
            if (!keepFollowers)
            {
                _npcs.Clear();
                return;
            }
            for (int index = _npcs.Count - 1; index >= 0; index--)
            {
                JxqyNpc npc = _npcs[index];
                if (npc.Kind == JxqyCharacterKind.Follower)
                {
                    npc.Stop();
                    continue;
                }
                _npcs.RemoveAt(index);
            }
        }

        public bool Remove(string name)
        {
            JxqyNpc found = Find(name);
            return found != null && _npcs.Remove(found);
        }

        public bool Remove(JxqyNpc npc)
        {
            return npc != null && _npcs.Remove(npc);
        }

        public JxqyNpc Find(string name)
        {
            return _npcs.Find(item =>
                string.Equals(
                    item.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<JxqyNpc> FindAll(string name)
        {
            return _npcs.FindAll(item =>
                string.Equals(
                    item.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }

        public bool IsObstacle(JxqyIntPoint tile, JxqyNpc except = null)
        {
            if (_player.TilePosition.Equals(tile))
                return true;
            return _npcs.Exists(item =>
                item != except &&
                item.Kind != JxqyCharacterKind.Flyer &&
                item.Life > 0 &&
                item.TilePosition.Equals(tile));
        }

        /// <summary>
        /// Reproduces the original Player.WalkTo/RunTo recovery path: when
        /// the player cannot reach the requested destination, every standing
        /// partner is asked to move toward that destination so it can clear a
        /// narrow passage. Partners remain normal collision obstacles.
        /// </summary>
        /// <returns>
        /// True when the original PartnerMoveTo rule requests that all
        /// partners be reset beside the player because the destination is
        /// more than twenty view tiles away.
        /// </returns>
        public bool MoveStandingFollowersTo(JxqyIntPoint destination)
        {
            if (_map.IsObstacleForCharacter(destination))
                return false;

            foreach (JxqyNpc npc in _npcs)
            {
                if (npc.Kind != JxqyCharacterKind.Follower ||
                    npc.Life <= 0 ||
                    !npc.IsStanding)
                {
                    continue;
                }

                int distance = JxqyPathfinder.GetViewTileDistance(
                    npc.TilePosition,
                    destination);
                if (distance > 20)
                    return true;
                if (distance <= 2)
                    continue;

                PathPlansLastTick++;
                PathPlansTotal++;
                IReadOnlyList<JxqyFloat2> path =
                    JxqyPathfinder.FindLegacyPath(
                        _map,
                        npc.TilePosition,
                        destination,
                        npc.PathType,
                        tile => _objects.IsObstacle(tile) ||
                                IsObstacle(tile, npc),
                        npc.DirectionCount);
                if (path.Count < 2 ||
                    !npc.BeginPath(path, distance > 5))
                {
                    continue;
                }

                npc.AmbientPath.Clear();
                npc.HasAmbientDestination = false;
                npc.Intent = JxqyNpcIntent.Move;
            }
            return false;
        }

        public JxqyCharacter FindClosestOpponent(JxqyNpc finder)
        {
            if (finder == null)
                throw new ArgumentNullException(nameof(finder));
            JxqyCharacter closest = null;
            int closestDistance = int.MaxValue;
            if (!finder.NoAutoAttackPlayer &&
                JxqyRelations.AreOpposed(finder, _player) &&
                JxqyPathfinder.CanViewTarget(
                    _map,
                    finder.TilePosition,
                    _player.TilePosition,
                    finder.VisionRadius))
            {
                closest = _player;
                closestDistance = JxqyPathfinder.GetViewTileDistance(
                    finder.TilePosition,
                    _player.TilePosition);
            }
            foreach (JxqyNpc candidate in _npcs)
            {
                if (candidate == finder || candidate.Life <= 0 ||
                    candidate.Kind == JxqyCharacterKind.Flyer &&
                    finder.Kind != JxqyCharacterKind.Flyer ||
                    candidate.Group == finder.Group &&
                    candidate.Relation == finder.Relation ||
                    !JxqyRelations.AreOpposed(finder, candidate) ||
                    !JxqyPathfinder.CanViewTarget(
                        _map,
                        finder.TilePosition,
                        candidate.TilePosition,
                        finder.VisionRadius))
                    continue;
                int distance = JxqyPathfinder.GetViewTileDistance(
                    finder.TilePosition,
                    candidate.TilePosition);
                if (distance >= closestDistance)
                    continue;
                closest = candidate;
                closestDistance = distance;
            }
            return closestDistance <= finder.VisionRadius ? closest : null;
        }

        public void Tick(float elapsedSeconds)
        {
            PathPlansLastTick = 0;
            foreach (JxqyNpc npc in _npcs)
            {
                if (npc.Life > 0)
                {
                    npc.BlindMilliseconds =
                        Math.Max(0, npc.BlindMilliseconds -
                                    elapsedSeconds * 1000f);
                    npc.AiRepathCooldownSeconds = Math.Max(
                        0f,
                        npc.AiRepathCooldownSeconds - elapsedSeconds);
                    UpdateAi(npc);
                }
                npc.TickMovement(elapsedSeconds, IsNpcMovementBlocked);
                if (npc.Life > 0)
                    RefreshApproachAfterWaypoint(npc);
            }
        }

        private void UpdateAi(JxqyNpc npc)
        {
            if (IsAiDisabled || npc.BlindMilliseconds > 0)
            {
                npc.FollowTarget = null;
                npc.Intent = JxqyNpcIntent.Idle;
                return;
            }

            if (npc.Kind == JxqyCharacterKind.Follower &&
                npc.TilePosition.Equals(_player.TilePosition))
            {
                // Invalid overlap can remain in an older save created before
                // live occupancy was checked while following. Move the
                // partner to a free neighboring tile once, then resume the
                // normal follower/auto-combat branches on the next tick.
                npc.StopMovementPreservingAction();
                npc.Intent = TryMoveAway(npc, _player, 1)
                    ? JxqyNpcIntent.Move
                    : JxqyNpcIntent.Idle;
                return;
            }

            if (npc.KeepAttackX > 0 || npc.KeepAttackY > 0)
            {
                npc.StopMovementPreservingAction();
                JxqyIntPoint attackWorld =
                    JxqyIsometricMapMath.TileToWorldPixel(
                        npc.KeepAttackX,
                        npc.KeepAttackY);
                npc.SetDirection(
                    new JxqyFloat2(
                        attackWorld.X,
                        attackWorld.Y) -
                    npc.PositionInWorld);
                npc.Intent = JxqyNpcIntent.Attack;
                return;
            }

            if (npc.DestinationMapPosX != 0 ||
                npc.DestinationMapPosY != 0)
            {
                var destination = new JxqyIntPoint(
                    npc.DestinationMapPosX,
                    npc.DestinationMapPosY);
                if (npc.TilePosition.Equals(destination))
                {
                    npc.DestinationMapPosX = 0;
                    npc.DestinationMapPosY = 0;
                    npc.Stop();
                    npc.Intent = JxqyNpcIntent.Idle;
                    return;
                }
                if (npc.IsWalking || npc.IsRunning)
                {
                    npc.Intent = JxqyNpcIntent.Move;
                    return;
                }
                if (TryBeginDestination(npc, destination))
                {
                    npc.Intent = JxqyNpcIntent.Move;
                    return;
                }
                npc.DestinationMapPosX = 0;
                npc.DestinationMapPosY = 0;
                npc.Intent = JxqyNpcIntent.Idle;
                return;
            }

            JxqyCharacter target = npc.FollowTarget;
            if (npc.IsFollowingScriptTarget)
            {
                if (target == null || target.Life <= 0)
                {
                    npc.Follow(null);
                    return;
                }

                int followDistance = JxqyPathfinder.GetViewTileDistance(
                    npc.TilePosition,
                    target.TilePosition);
                if (followDistance <= 1)
                {
                    npc.Stop();
                    npc.Intent = JxqyNpcIntent.Idle;
                    return;
                }

                if (npc.IsWalking || npc.IsRunning)
                {
                    npc.Intent = JxqyNpcIntent.Move;
                    return;
                }
                TryBeginApproach(npc, target);
                return;
            }

            if (npc.Kind == JxqyCharacterKind.AfraidPlayerAnimal)
            {
                UpdateAfraidAnimal(npc);
                return;
            }
            if (npc.Kind != JxqyCharacterKind.Fighter &&
                npc.Kind != JxqyCharacterKind.Follower)
            {
                UpdateAmbientMovement(npc);
                return;
            }

            if (target != null &&
                JxqyPathfinder.GetViewTileDistance(
                    npc.TilePosition,
                    target.TilePosition) > npc.VisionRadius)
            {
                npc.FollowTarget = target = null;
            }
            if (target == null || target.Life <= 0 ||
                !JxqyRelations.AreOpposed(npc, target))
            {
                target = npc.FollowTarget = null;
            }
            if (target == null && !npc.StopFindingTarget)
                target = npc.FollowTarget = FindClosestOpponent(npc);
            if (target == null)
            {
                if (npc.Kind == JxqyCharacterKind.Follower)
                {
                    int playerDistance =
                        JxqyPathfinder.GetViewTileDistance(
                            npc.TilePosition,
                            _player.TilePosition);
                    if (playerDistance > 2)
                    {
                        // Original Character.PartnerMoveTo runs when the
                        // partner falls more than five tiles behind and keeps
                        // running in the two-to-five tile band. A walking
                        // route must be upgraded immediately when the player
                        // accelerates away instead of waiting for that stale
                        // route to finish.
                        bool run = playerDistance > 5 || npc.IsRunning;
                        if ((!npc.IsWalking && !npc.IsRunning) ||
                            run && !npc.IsRunning)
                        {
                            if (npc.IsWalking)
                                npc.StopMovementPreservingAction();
                            TryBeginApproach(npc, _player, run);
                        }
                        return;
                    }
                }
                UpdateAmbientMovement(npc);
                return;
            }

            npc.AmbientPath.Clear();
            npc.HasAmbientDestination = false;

            // Original Character.Attacking is gated by PerformActionOk().
            // Once a ranged NPC has started its approach/retreat action, it
            // finishes that movement before choosing another attack position.
            // Re-evaluating the close-range branch on every AI tick replaces
            // the retreat path continuously, which makes archers keep running
            // and their locomotion animation/facing appear unstable.
            if (npc.IsWalking || npc.IsRunning)
            {
                npc.Intent = JxqyNpcIntent.Move;
                return;
            }

            int distance = JxqyPathfinder.GetViewTileDistance(
                npc.TilePosition,
                target.TilePosition);
            int attackDistance =
                GetPreferredAttackDistance(npc, distance);
            if (distance == attackDistance)
            {
                // Original AttackingIsOk ultimately performs the selected
                // attack when the actor is already at the exact use distance,
                // even if CanViewTarget rejected the target tile. The magic
                // projectile still performs its own obstacle collision. If AI
                // continues pathfinding here, a melee NPC tries to enter the
                // player's occupied tile and circles around it indefinitely.
                // Reaching attack range only cancels locomotion. Calling Stop()
                // here every tick also overwrites Attack/Hurt with FightStand,
                // cutting every non-looping combat animation short.
                npc.StopMovementPreservingAction();
                npc.Intent = JxqyNpcIntent.Attack;
                return;
            }
            if (distance < attackDistance)
            {
                if (TryMoveAway(
                        npc,
                        target,
                        attackDistance - distance))
                {
                    npc.Intent = JxqyNpcIntent.Move;
                    return;
                }
                npc.StopMovementPreservingAction();
                npc.Intent = JxqyNpcIntent.Attack;
                return;
            }

            TryBeginApproach(npc, target);
        }

        private static int GetPreferredAttackDistance(
            JxqyNpc npc,
            int targetDistance)
        {
            var distances = new List<int>
            {
                Math.Max(1, npc.AttackRadius),
            };
            distances.AddRange(
                npc.AdditionalBasicMagics
                    .Select(item => Math.Max(1, item.Distance)));
            int nearestDifference = distances.Min(value =>
                Math.Abs(value - targetDistance));
            return distances.First(value =>
                Math.Abs(value - targetDistance) ==
                nearestDifference);
        }

        private bool IsNpcMovementBlocked(
            JxqyCharacter character,
            JxqyIntPoint tile)
        {
            return _map.IsObstacleForCharacter(tile) ||
                   _objects.IsObstacle(tile) ||
                   IsObstacle(tile, character as JxqyNpc);
        }

        private bool TryMoveAway(
            JxqyNpc npc,
            JxqyCharacter target,
            int tileDistance)
        {
            if (tileDistance < 1)
                return false;

            // Match Character.MoveAwayTarget from the original engine: pick
            // the discrete map direction from the world-space vector away
            // from the target, then retreat the requested number of tiles in
            // that direction. Selecting the first equally-distant neighbor
            // biases every ranged NPC toward the map's upper-left corner.
            JxqyFloat2 away =
                npc.PositionInWorld - target.PositionInWorld;
            JxqyIntPoint destination = npc.TilePosition;
            if (away == JxqyFloat2.Zero)
            {
                // Older saves can contain a follower on the player's exact
                // tile. There is no meaningful away vector in that state, so
                // separate the actors through the first free neighboring tile.
                bool found = false;
                foreach (JxqyIntPoint candidate in
                         EnumerateNeighborTiles(npc.TilePosition))
                {
                    if (_map.IsObstacleForCharacter(candidate) ||
                        _objects.IsObstacle(candidate) ||
                        IsObstacle(candidate, npc))
                    {
                        continue;
                    }
                    destination = candidate;
                    found = true;
                    break;
                }
                if (!found)
                    return false;
            }
            else
            {
                int direction = JxqyDirection.GetIndex(away, 8);
                for (int step = 0; step < tileDistance; step++)
                {
                    destination = JxqyPathfinder.GetAllNeighbors(
                        destination)[direction];
                }
            }
            if (_map.IsObstacleForCharacter(destination) ||
                _objects.IsObstacle(destination) ||
                IsObstacle(destination, npc))
            {
                return false;
            }

            PathPlansLastTick++;
            PathPlansTotal++;
            IReadOnlyList<JxqyFloat2> path =
                JxqyPathfinder.FindLegacyPath(
                    _map,
                    npc.TilePosition,
                    destination,
                    npc.PathType,
                    tile => _objects.IsObstacle(tile) ||
                            IsObstacle(tile, npc),
                    npc.DirectionCount);
            return path.Count >= 2 && npc.BeginPath(path);
        }

        private void UpdateAfraidAnimal(JxqyNpc npc)
        {
            int distance = JxqyPathfinder.GetViewTileDistance(
                npc.TilePosition,
                _player.TilePosition);
            if (distance >= npc.VisionRadius)
            {
                UpdateAmbientMovement(npc);
                return;
            }
            if (npc.IsWalking || npc.IsRunning)
            {
                npc.Intent = JxqyNpcIntent.Flee;
                return;
            }
            JxqyIntPoint best = npc.TilePosition;
            int bestDistance = distance;
            foreach (JxqyIntPoint candidate in
                     EnumerateNeighborTiles(npc.TilePosition))
            {
                if (_map.IsObstacleForCharacter(candidate) ||
                    _objects.IsObstacle(candidate) ||
                    IsObstacle(candidate, npc))
                {
                    continue;
                }
                int candidateDistance =
                    JxqyPathfinder.GetViewTileDistance(
                        candidate,
                        _player.TilePosition);
                if (candidateDistance <= bestDistance)
                    continue;
                best = candidate;
                bestDistance = candidateDistance;
            }
            if (!best.Equals(npc.TilePosition) &&
                TryBeginMoveToTile(npc, best))
            {
                npc.Intent = JxqyNpcIntent.Flee;
                return;
            }
            npc.Intent = JxqyNpcIntent.Idle;
        }

        private void UpdateAmbientMovement(JxqyNpc npc)
        {
            if (npc.IsWalking || npc.IsRunning)
            {
                npc.Intent = JxqyNpcIntent.Move;
                return;
            }

            if (npc.HasAmbientDestination)
            {
                if (npc.TilePosition.Equals(npc.AmbientDestination))
                {
                    npc.HasAmbientDestination = false;
                    npc.Intent = JxqyNpcIntent.Idle;
                    return;
                }
                if (TryBeginAmbientDestination(
                        npc,
                        npc.AmbientDestination))
                {
                    npc.Intent = JxqyNpcIntent.Move;
                    return;
                }
                npc.HasAmbientDestination = false;
                npc.Intent = JxqyNpcIntent.Idle;
                return;
            }

            if (npc.Action != 1 && npc.Action != 2)
            {
                npc.Intent = JxqyNpcIntent.Idle;
                return;
            }
            int denominator =
                npc.Kind == JxqyCharacterKind.Flyer ? 20 : 400;
            if (_random.Next(0, denominator) != 0)
            {
                npc.Intent = JxqyNpcIntent.Idle;
                return;
            }

            JxqyIntPoint destination;
            if (npc.Action == 2)
            {
                if (npc.FixedPath.Count < 2)
                {
                    npc.Intent = JxqyNpcIntent.Idle;
                    return;
                }
                npc.CurrentFixedPositionIndex++;
                if (npc.CurrentFixedPositionIndex >= npc.FixedPath.Count)
                    npc.CurrentFixedPositionIndex = 0;
                destination =
                    npc.FixedPath[npc.CurrentFixedPositionIndex];
            }
            else
            {
                EnsureAmbientPath(npc);
                if (npc.AmbientPath.Count < 2)
                {
                    npc.Intent = JxqyNpcIntent.Idle;
                    return;
                }
                destination = npc.AmbientPath[
                    _random.Next(0, npc.AmbientPath.Count)];
            }

            npc.AmbientDestination = destination;
            npc.HasAmbientDestination = true;
            if (TryBeginAmbientDestination(npc, destination))
            {
                npc.Intent = JxqyNpcIntent.Move;
                return;
            }
            npc.HasAmbientDestination = false;
            npc.Intent = JxqyNpcIntent.Idle;
        }

        private void EnsureAmbientPath(JxqyNpc npc)
        {
            if (npc.AmbientPath.Count > 0)
                return;
            npc.AmbientPath.Add(npc.TilePosition);
            int maximumOffset = npc.Kind == JxqyCharacterKind.Flyer
                ? 15
                : 10;
            int attemptsRemaining = 21;
            while (npc.AmbientPath.Count < 8 && attemptsRemaining-- > 0)
            {
                var candidate = new JxqyIntPoint(
                    npc.TilePosition.X +
                    _random.Next(-maximumOffset, maximumOffset),
                    npc.TilePosition.Y +
                    _random.Next(-maximumOffset, maximumOffset));
                if (candidate.Equals(new JxqyIntPoint(0, 0)) ||
                    candidate.X < 0 || candidate.X >= _map.Columns ||
                    candidate.Y < 0 || candidate.Y >= _map.Rows)
                {
                    continue;
                }
                IReadOnlyList<JxqyFloat2> line =
                    JxqyPathfinder.FindLegacyPath(
                        _map,
                        npc.TilePosition,
                        candidate,
                        JxqyPathType.PathStraightLine);
                if (line.Count < 2 ||
                    npc.Kind != JxqyCharacterKind.Flyer &&
                    line.Any(point => _map.IsObstacleForCharacter(
                        JxqyIsometricMapMath.WorldPixelToTile(
                            (int)point.X,
                            (int)point.Y))))
                {
                    continue;
                }
                npc.AmbientPath.Add(candidate);
            }
        }

        private bool TryBeginAmbientDestination(
            JxqyNpc npc,
            JxqyIntPoint destination)
        {
            PathPlansLastTick++;
            PathPlansTotal++;
            IReadOnlyList<JxqyFloat2> path =
                JxqyPathfinder.FindLegacyPath(
                    _map,
                    npc.TilePosition,
                    destination,
                    npc.PathType,
                    tile => _objects.IsObstacle(tile) ||
                            IsObstacle(tile, npc),
                    npc.DirectionCount);
            return path.Count >= 2 && npc.BeginPath(path);
        }

        private bool TryBeginMoveToTile(
            JxqyNpc npc,
            JxqyIntPoint destination)
        {
            JxqyIntPoint world =
                JxqyIsometricMapMath.TileToWorldPixel(
                    destination.X,
                    destination.Y);
            return npc.BeginPath(
                new[]
                {
                    npc.PositionInWorld,
                    new JxqyFloat2(world.X, world.Y),
                });
        }

        private static IEnumerable<JxqyIntPoint> EnumerateNeighborTiles(
            JxqyIntPoint center)
        {
            for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                for (int columnOffset = -1;
                     columnOffset <= 1;
                     columnOffset++)
                {
                    if (columnOffset == 0 && rowOffset == 0)
                        continue;
                    yield return new JxqyIntPoint(
                        center.X + columnOffset,
                        center.Y + rowOffset);
                }
            }
        }

        private void TryBeginApproach(
            JxqyNpc npc,
            JxqyCharacter target,
            bool run = false)
        {
            if (npc.AiRepathCooldownSeconds > 0f)
            {
                npc.Intent = JxqyNpcIntent.Idle;
                return;
            }

            PathPlansLastTick++;
            PathPlansTotal++;
            IReadOnlyList<JxqyFloat2> path =
                JxqyPathfinder.FindLegacyPath(
                    _map,
                    npc.TilePosition,
                    target.TilePosition,
                    npc.PathType,
                    tile => _objects.IsObstacle(tile) ||
                            IsObstacle(tile, npc),
                    npc.DirectionCount);
            if (path.Count >= 2 && npc.BeginPath(path, run))
            {
                RememberPlannedWaypoint(npc);
                npc.Intent = JxqyNpcIntent.Move;
                return;
            }

            npc.AiRepathCooldownSeconds = FailedPathRetrySeconds;
            npc.Intent = JxqyNpcIntent.Idle;
        }

        private bool TryBeginDestination(
            JxqyNpc npc,
            JxqyIntPoint destination)
        {
            if (npc.AiRepathCooldownSeconds > 0f)
            {
                return false;
            }
            PathPlansLastTick++;
            PathPlansTotal++;
            IReadOnlyList<JxqyFloat2> path =
                JxqyPathfinder.FindLegacyPath(
                    _map,
                    npc.TilePosition,
                    destination,
                    JxqyPathType.PerfectMaxPlayerTry,
                    tile => _objects.IsObstacle(tile) ||
                            IsObstacle(tile, npc),
                    npc.DirectionCount,
                    disableMaximumExpandedNodes: true);
            if (path.Count >= 2 &&
                npc.BeginPath(path))
            {
                RememberPlannedWaypoint(npc);
                return true;
            }
            npc.AiRepathCooldownSeconds = FailedPathRetrySeconds;
            return false;
        }

        private void RefreshApproachAfterWaypoint(JxqyNpc npc)
        {
            if (!npc.HasAiPlannedNextTile ||
                (!npc.IsWalking && !npc.IsRunning) ||
                npc.FollowTarget == null ||
                npc.FollowTarget.Life <= 0 ||
                npc.NextPathTilePosition.Equals(npc.AiPlannedNextTile))
            {
                return;
            }

            // Npc.FollowTargetFound sets MoveTargetChanged in the original.
            // Character therefore recalculates at the next reached waypoint,
            // even if the target itself did not move.
            npc.StopMovementPreservingAction();
            npc.HasAiPlannedNextTile = false;
            // MoveTargetChanged in the original routes the actor back through
            // AttackingIsOk at every reached waypoint. Replanning only the
            // approach skips the newly reached attack-range check and makes a
            // melee NPC orbit the target's occupied tile forever.
            UpdateAi(npc);
        }

        private static void RememberPlannedWaypoint(JxqyNpc npc)
        {
            npc.AiPlannedNextTile = npc.NextPathTilePosition;
            npc.HasAiPlannedNextTile = true;
        }
    }

    public static class JxqyRelations
    {
        public static bool AreOpposed(
            JxqyCharacter left,
            JxqyCharacter right)
        {
            if (left == null || right == null || left == right)
                return false;
            if (right.Relation == JxqyRelationType.Enemy)
            {
                return left.Kind == JxqyCharacterKind.Player ||
                       left.Relation == JxqyRelationType.Friend ||
                       left.Relation == JxqyRelationType.None;
            }
            if (right.Kind == JxqyCharacterKind.Player ||
                right.Relation == JxqyRelationType.Friend)
            {
                return left.Relation == JxqyRelationType.Enemy ||
                       left.Relation == JxqyRelationType.None;
            }
            if (right.Relation == JxqyRelationType.None)
            {
                return left.Kind == JxqyCharacterKind.Player ||
                       left.Relation == JxqyRelationType.Friend ||
                       left.Relation == JxqyRelationType.Enemy;
            }
            return false;
        }
    }

    public sealed class JxqyTrapRegistry
    {
        private readonly Dictionary<string, Dictionary<int, string>> _traps =
            new Dictionary<string, Dictionary<int, string>>(
                StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _ignoredIndices = new HashSet<int>();

        public IReadOnlyCollection<int> IgnoredIndices => _ignoredIndices;
        public bool HasEntries
        {
            get
            {
                foreach (Dictionary<int, string> mapTraps in _traps.Values)
                {
                    if (mapTraps.Count > 0)
                        return true;
                }

                return false;
            }
        }

        public void SetTrap(
            string mapName,
            int index,
            string scriptAddress,
            bool activate = true)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                throw new ArgumentException(
                    "Map name is required.",
                    nameof(mapName));
            if (index <= 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (!_traps.TryGetValue(
                    mapName,
                    out Dictionary<int, string> mapTraps))
            {
                mapTraps = new Dictionary<int, string>();
                _traps[mapName] = mapTraps;
            }
            if (string.IsNullOrWhiteSpace(scriptAddress))
                mapTraps.Remove(index);
            else
                mapTraps[index] = scriptAddress;
            if (activate)
                _ignoredIndices.Remove(index);
        }

        public bool TryTrigger(
            string mapName,
            int index,
            out string scriptAddress)
        {
            scriptAddress = string.Empty;
            if (index <= 0 || _ignoredIndices.Contains(index) ||
                string.IsNullOrWhiteSpace(mapName) ||
                !_traps.TryGetValue(
                    mapName,
                    out Dictionary<int, string> mapTraps) ||
                !mapTraps.TryGetValue(index, out string address) ||
                string.IsNullOrWhiteSpace(address))
                return false;
            _ignoredIndices.Add(index);
            scriptAddress = address;
            return true;
        }

        public void Rearm(int index)
        {
            _ignoredIndices.Remove(index);
        }

        public void ClearTriggered()
        {
            _ignoredIndices.Clear();
        }

        public void SetTriggered(int index, bool triggered)
        {
            if (index <= 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (triggered)
                _ignoredIndices.Add(index);
            else
                _ignoredIndices.Remove(index);
        }

        public void ForEach(
            Action<string, int, string, bool> visitor)
        {
            if (visitor == null)
                throw new ArgumentNullException(nameof(visitor));
            foreach (KeyValuePair<
                         string,
                         Dictionary<int, string>> map in _traps)
            {
                foreach (KeyValuePair<int, string> trap in map.Value)
                {
                    visitor(
                        map.Key,
                        trap.Key,
                        trap.Value,
                        _ignoredIndices.Contains(trap.Key));
                }
            }
        }

        public JxqyTrapRegistry Clone()
        {
            var clone = new JxqyTrapRegistry();
            foreach (KeyValuePair<
                         string,
                         Dictionary<int, string>> map in _traps)
            {
                foreach (KeyValuePair<int, string> trap in map.Value)
                {
                    clone.SetTrap(
                        map.Key,
                        trap.Key,
                        trap.Value,
                        activate: false);
                }
            }
            foreach (int index in _ignoredIndices)
                clone._ignoredIndices.Add(index);
            return clone;
        }
    }

    public sealed class JxqyMapTrapController
    {
        private readonly IJxqyTileCollisionMap _map;
        private readonly JxqyTrapRegistry _registry;

        public JxqyMapTrapController(
            IJxqyTileCollisionMap map,
            JxqyTrapRegistry registry)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _registry = registry ??
                        throw new ArgumentNullException(nameof(registry));
        }

        public bool TryEnter(
            JxqyCharacter character,
            string mapName,
            out string scriptAddress)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            int index = _map.GetTrapIndex(character.TilePosition);
            if (!_registry.TryTrigger(mapName, index, out scriptAddress))
                return false;
            character.Stop();
            return true;
        }
    }

    public readonly struct JxqyAlphaMask
    {
        public JxqyAlphaMask(int width, int height, byte[] alpha)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (alpha == null)
                throw new ArgumentNullException(nameof(alpha));
            if (alpha.Length != checked(width * height))
                throw new ArgumentException(
                    "Alpha data size does not match dimensions.",
                    nameof(alpha));
            Width = width;
            Height = height;
            Alpha = alpha;
        }

        public int Width { get; }
        public int Height { get; }
        public byte[] Alpha { get; }
    }

    public static class JxqyCollision
    {
        public static bool BoxesOverlap(
            JxqyIntRect left,
            JxqyIntRect right)
        {
            return left.X < right.Right &&
                   left.Right > right.X &&
                   left.Y < right.Bottom &&
                   left.Bottom > right.Y;
        }

        public static bool AlphaMasksOverlap(
            JxqyIntRect leftRegion,
            JxqyAlphaMask left,
            JxqyIntRect rightRegion,
            JxqyAlphaMask right)
        {
            int startX = Math.Max(leftRegion.X, rightRegion.X);
            int startY = Math.Max(leftRegion.Y, rightRegion.Y);
            int endX = Math.Min(leftRegion.Right, rightRegion.Right);
            int endY = Math.Min(leftRegion.Bottom, rightRegion.Bottom);
            if (startX >= endX || startY >= endY)
                return false;
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int leftIndex =
                        x - leftRegion.X +
                        (y - leftRegion.Y) * left.Width;
                    int rightIndex =
                        x - rightRegion.X +
                        (y - rightRegion.Y) * right.Width;
                    if (left.Alpha[leftIndex] != 0 &&
                        right.Alpha[rightIndex] != 0)
                        return true;
                }
            }
            return false;
        }
    }
}
