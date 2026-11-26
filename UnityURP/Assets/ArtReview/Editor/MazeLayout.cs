using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class MazeLayout
{
    public const int Size = 17;
    public const float Pitch = 4f;
    public const int TargetSteps = 160;
    public const int Start = 0;
    private const int Cells = Size * Size;
    public static readonly Vector2Int[] Directions =
        { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

    private readonly int[] openings = new int[Cells];
    public int Exit { get; private set; }
    public int Loops { get; private set; }
    public IReadOnlyList<int> Route { get; private set; }
    public int Seed { get; private set; }
    public readonly List<int[]> Districts = new();

    public static MazeLayout Create()
    {
        MazeLayout best = null;
        int error = int.MaxValue;
        for (int seed = 453; seed < 453 + 128 && error != 0; seed++)
        {
            var maze = new MazeLayout { Seed = seed };
            maze.Carve(new System.Random(seed));
            int[] distances = maze.Trace(out _);
            foreach (int candidate in Enumerable.Range(0, Cells).Where(IsBoundary))
            {
                int delta = Math.Abs(distances[candidate] - TargetSteps);
                if (delta >= error)
                    continue;
                error = delta;
                maze.Exit = candidate;
                best = maze;
            }
        }
        if (best == null || error > 4)
            throw new InvalidOperationException("No maze layout met the walking distance target.");
        best.AddLoops(new System.Random(best.Seed + 95));
        best.Route = best.GetRoute();
        best.CreateDistricts();
        return best;
    }

    private void CreateDistricts()
    {
        int length = Trace(out _)[Exit];
        var occupied = new HashSet<int>();
        var forbidden = new HashSet<int>();
        foreach (int station in new[] { 0, 18, 45, 76, 108, 139, 153, 154, 155, 156, 157, 158, 159, 160 })
            foreach (int cell in Route.Skip(Math.Max(0, station - 1)).Take(3)) forbidden.Add(cell);
        foreach (int station in new[] { 36, 85, 124, 22, 67, 111 })
        {
            bool found = false;
            int[] travel = Enumerable.Repeat(-1, Cells).ToArray();
            var pending = new Queue<int>(); pending.Enqueue(Route[station]); travel[Route[station]]=0;
            while (pending.Count > 0)
            {
                int current = pending.Dequeue();
                for (int side=0; side<4; side++)
                {
                    int next = Neighbor(current,side);
                    if (!IsOpen(current,side) || next<0 || travel[next]>=0) continue;
                    travel[next]=travel[current]+1;pending.Enqueue(next);
                }
            }
            var candidates = Enumerable.Range(0, Cells).Where(c => c % Size < Size - 1 && c / Size < Size - 1)
                .Select(c => new[] { c, c + 1, c + Size, c + Size + 1 })
                .Where(group => !group.Any(c => forbidden.Contains(c) || occupied.Contains(c)))
                .OrderBy(group => group.Min(c => travel[c]));
            foreach (int[] group in candidates)
            {
                var changed = new List<(int cell, int side)>();
                foreach (int c in group)
                    foreach (int side in new[] { 0, 1 })
                        if (group.Contains(Neighbor(c, side)) && !IsOpen(c, side))
                        { SetOpen(c, side, true); changed.Add((c, side)); }
                if (Trace(out _)[Exit] != length)
                {
                    foreach (var edge in changed) SetOpen(edge.cell, edge.side, false);
                    continue;
                }
                Districts.Add(group); occupied.UnionWith(group); found = true;
                break;
            }
            if (!found) throw new InvalidOperationException("Cannot place an independent room without shortening the maze.");
        }
    }

    public static int Neighbor(int cell, int direction)
    {
        Vector2Int delta = Directions[direction];
        int x = cell % Size + delta.x;
        int z = cell / Size + delta.y;
        return x >= 0 && x < Size && z >= 0 && z < Size ? z * Size + x : -1;
    }

    public static Vector3 Center(int cell) => new Vector3((cell % Size + .5f) * Pitch, 0, (cell / Size + .5f) * Pitch);

    public bool IsOpen(int cell, int direction) => (openings[cell] & (1 << direction)) != 0;
    public int[] CopyPassages() => (int[])openings.Clone();

    private static bool IsBoundary(int cell) =>
        cell % Size == 0 || cell % Size == Size - 1 || cell / Size == 0 || cell / Size == Size - 1;

    private void SetOpen(int cell, int direction, bool open)
    {
        int other = Neighbor(cell, direction);
        int opposite = (direction + 2) % 4;
        if (open)
        {
            openings[cell] |= 1 << direction;
            openings[other] |= 1 << opposite;
        }
        else
        {
            openings[cell] &= ~(1 << direction);
            openings[other] &= ~(1 << opposite);
        }
    }

    private void Carve(System.Random random)
    {
        var visited = new bool[Cells];
        var path = new Stack<int>();
        visited[Start] = true;
        path.Push(Start);
        while (path.TryPeek(out int cell))
        {
            int[] options = Enumerable.Range(0, 4).Where(direction =>
            {
                int next = Neighbor(cell, direction);
                return next >= 0 && !visited[next];
            }).ToArray();
            if (options.Length == 0)
            {
                path.Pop();
                continue;
            }
            int selected = options[random.Next(options.Length)];
            int nextCell = Neighbor(cell, selected);
            visited[nextCell] = true;
            SetOpen(cell, selected, true);
            path.Push(nextCell);
        }
    }

    private void AddLoops(System.Random random)
    {
        int original = Trace(out _)[Exit];
        var candidates = new List<(int cell, int direction)>();
        for (int cell = 0; cell < Cells; cell++)
        {
            for (int direction = 0; direction < 2; direction++)
            {
                if (!IsOpen(cell, direction) && Neighbor(cell, direction) >= 0)
                    candidates.Add((cell, direction));
            }
        }
        foreach (var candidate in candidates.OrderBy(_ => random.Next()))
        {
            SetOpen(candidate.cell, candidate.direction, true);
            if (Trace(out _)[Exit] == original)
            {
                Loops++;
                if (Loops == 15)
                    return;
            }
            else
            {
                SetOpen(candidate.cell, candidate.direction, false);
            }
        }
    }

    public int[] Trace(out int[] parents)
    {
        int[] distance = Enumerable.Repeat(-1, Cells).ToArray();
        parents = Enumerable.Repeat(-1, Cells).ToArray();
        var queue = new Queue<int>();
        distance[Start] = 0;
        queue.Enqueue(Start);
        while (queue.TryDequeue(out int cell))
        {
            for (int direction = 0; direction < 4; direction++)
            {
                if (!IsOpen(cell, direction))
                    continue;
                int next = Neighbor(cell, direction);
                if (distance[next] >= 0)
                    continue;
                distance[next] = distance[cell] + 1;
                parents[next] = cell;
                queue.Enqueue(next);
            }
        }
        return distance;
    }

    private List<int> GetRoute()
    {
        Trace(out int[] parents);
        var route = new List<int>();
        for (int cell = Exit; cell >= 0; cell = parents[cell])
            route.Add(cell);
        route.Reverse();
        return route;
    }
}
