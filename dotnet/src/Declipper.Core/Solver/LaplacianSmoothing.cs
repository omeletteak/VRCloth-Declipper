using System.Collections.Generic;
using System.Numerics;

namespace Declipper.Core.Solver
{
    /// <summary>
    /// Uniform-weight Laplacian smoothing of a per-vertex vector field,
    /// restricted to a vertex region. The solver smooths the push-out
    /// displacement field (not vertex positions), so the original cloth
    /// detail is preserved and only the correction is blended; vertices
    /// outside the region keep their field value (zero for untouched ones)
    /// and act as natural anchors.
    ///
    /// Ported from Assets/VRCloth-Declipper/Core/LaplacianSmoothing.cs.
    /// </summary>
    public static class LaplacianSmoothing
    {
        /// <summary>
        /// The seed vertices grown outward by <paramref name="rings"/> neighbor
        /// hops, returned as cluster representatives — the region smoothing is
        /// allowed to touch. Seeds are raw vertex indices and are mapped to
        /// their representatives first.
        /// </summary>
        public static HashSet<int> ExpandRegion(VertexAdjacency adjacency, IEnumerable<int> seeds, int rings)
        {
            var region = new HashSet<int>();
            if (adjacency == null || seeds == null)
            {
                return region;
            }

            var frontier = new List<int>();
            foreach (int seed in seeds)
            {
                int rep = adjacency.RepresentativeOf(seed);
                if (region.Add(rep))
                {
                    frontier.Add(rep);
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                var next = new List<int>();
                foreach (int rep in frontier)
                {
                    var neighbors = adjacency.NeighborsOf(rep);
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        if (region.Add(neighbors[n]))
                        {
                            next.Add(neighbors[n]);
                        }
                    }
                }
                frontier = next;
            }
            return region;
        }

        /// <summary>
        /// Pulls every region vertex's field value toward the average of its
        /// neighbors' values: f' = Lerp(f, neighborAverage, lambda), repeated
        /// <paramref name="iterations"/> times. Neighbors outside the region
        /// participate in the average but never change, anchoring the
        /// boundary. Welded clones are written together.
        /// </summary>
        public static void Smooth(
            Vector3[] displacements, VertexAdjacency adjacency, HashSet<int> region, float lambda, int iterations)
        {
            if (displacements == null || adjacency == null || region == null || region.Count == 0)
            {
                return;
            }

            var updates = new List<KeyValuePair<int, Vector3>>(region.Count);
            for (int i = 0; i < iterations; i++)
            {
                updates.Clear();
                foreach (int rep in region)
                {
                    var neighbors = adjacency.NeighborsOf(rep);
                    if (neighbors.Count == 0)
                    {
                        continue;
                    }
                    Vector3 sum = Vector3.Zero;
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        sum += displacements[neighbors[n]];
                    }
                    Vector3 smoothed = Vector3.Lerp(displacements[rep], sum / neighbors.Count, lambda);
                    updates.Add(new KeyValuePair<int, Vector3>(rep, smoothed));
                }

                foreach (var update in updates)
                {
                    var clones = adjacency.MembersOf(update.Key);
                    for (int m = 0; m < clones.Count; m++)
                    {
                        displacements[clones[m]] = update.Value;
                    }
                }
            }
        }
    }
}
