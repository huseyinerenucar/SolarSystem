using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Barnes-Hut Octree for efficient N-body gravity calculations
/// Reduces complexity from O(n²) to O(n log n)
/// Groups distant bodies into cells to approximate gravitational forces
/// </summary>
public class BarnesHutOctree
{
    /// <summary>
    /// Theta threshold for force approximation
    /// Lower = more accurate but slower (0.5 is typical)
    /// Higher = faster but less accurate
    /// </summary>
    public float theta = 0.5f;

    private OctreeNode root;
    private Vector3D boundsCenter;
    private double boundsSize;

    public class OctreeNode
    {
        // Spatial bounds
        public Vector3D center;
        public double size;

        // Physics data
        public Vector3D centerOfMass;
        public double totalMass;

        // Body reference (for leaf nodes)
        public CelestialBody body;

        // Children (null if leaf node)
        public OctreeNode[] children;

        // Is this node a leaf with a single body?
        public bool IsLeaf => body != null;
        public bool IsEmpty => totalMass == 0;

        public OctreeNode(Vector3D center, double size)
        {
            this.center = center;
            this.size = size;
            this.centerOfMass = Vector3D.zero;
            this.totalMass = 0;
            this.body = null;
            this.children = null;
        }

        /// <summary>
        /// Determine which octant a position falls into
        /// </summary>
        public int GetOctant(Vector3D position)
        {
            int octant = 0;
            if (position.x >= center.x) octant |= 4;
            if (position.y >= center.y) octant |= 2;
            if (position.z >= center.z) octant |= 1;
            return octant;
        }

        /// <summary>
        /// Get the center of a specific octant
        /// </summary>
        public Vector3D GetOctantCenter(int octant)
        {
            double offset = size * 0.25;
            return new Vector3D(
                center.x + ((octant & 4) != 0 ? offset : -offset),
                center.y + ((octant & 2) != 0 ? offset : -offset),
                center.z + ((octant & 1) != 0 ? offset : -offset)
            );
        }

        /// <summary>
        /// Subdivide this node into 8 children
        /// </summary>
        public void Subdivide()
        {
            if (children != null) return;

            children = new OctreeNode[8];
            double childSize = size * 0.5;

            for (int i = 0; i < 8; i++)
            {
                children[i] = new OctreeNode(GetOctantCenter(i), childSize);
            }
        }
    }

    /// <summary>
    /// Build the octree from a collection of celestial bodies
    /// </summary>
    public void Build(CelestialBody[] bodies)
    {
        if (bodies.Length == 0) return;

        // Calculate bounds that encompass all bodies
        CalculateBounds(bodies);

        // Create root node
        root = new OctreeNode(boundsCenter, boundsSize);

        // Insert all bodies
        foreach (var body in bodies)
        {
            Insert(root, body);
        }
    }

    /// <summary>
    /// Calculate bounds that encompass all bodies
    /// </summary>
    private void CalculateBounds(CelestialBody[] bodies)
    {
        Vector3D min = new Vector3D(double.MaxValue, double.MaxValue, double.MaxValue);
        Vector3D max = new Vector3D(double.MinValue, double.MinValue, double.MinValue);

        foreach (var body in bodies)
        {
            Vector3D pos = body.positionD;
            min = new Vector3D(
                System.Math.Min(min.x, pos.x),
                System.Math.Min(min.y, pos.y),
                System.Math.Min(min.z, pos.z)
            );
            max = new Vector3D(
                System.Math.Max(max.x, pos.x),
                System.Math.Max(max.y, pos.y),
                System.Math.Max(max.z, pos.z)
            );
        }

        boundsCenter = (min + max) * 0.5;
        Vector3D size = max - min;
        boundsSize = System.Math.Max(System.Math.Max(size.x, size.y), size.z) * 1.1; // Add 10% margin
    }

    /// <summary>
    /// Recursively insert a body into the octree
    /// </summary>
    private void Insert(OctreeNode node, CelestialBody body)
    {
        // If node is empty, make it a leaf
        if (node.totalMass == 0)
        {
            node.body = body;
            node.totalMass = body.mass;
            node.centerOfMass = body.positionD;
            return;
        }

        // Update center of mass (weighted average)
        double newTotalMass = node.totalMass + body.mass;
        node.centerOfMass = (node.centerOfMass * node.totalMass + body.positionD * body.mass) / newTotalMass;
        node.totalMass = newTotalMass;

        // If this is a leaf with an existing body, subdivide
        if (node.IsLeaf)
        {
            CelestialBody existingBody = node.body;
            node.body = null;
            node.Subdivide();

            // Re-insert the existing body
            int existingOctant = node.GetOctant(existingBody.positionD);
            Insert(node.children[existingOctant], existingBody);
        }

        // Insert the new body into appropriate child
        if (node.children != null)
        {
            int octant = node.GetOctant(body.positionD);
            Insert(node.children[octant], body);
        }
    }

    /// <summary>
    /// Calculate gravitational acceleration on a body using Barnes-Hut approximation
    /// </summary>
    public Vector3D CalculateAcceleration(CelestialBody body)
    {
        if (root == null || root.IsEmpty) return Vector3D.zero;
        return CalculateAccelerationRecursive(root, body);
    }

    /// <summary>
    /// Recursively calculate acceleration using Barnes-Hut approximation
    /// </summary>
    private Vector3D CalculateAccelerationRecursive(OctreeNode node, CelestialBody body)
    {
        // Skip empty nodes
        if (node.IsEmpty) return Vector3D.zero;

        // Don't calculate force on self
        if (node.IsLeaf && node.body == body) return Vector3D.zero;

        Vector3D direction = node.centerOfMass - body.positionD;
        double distanceSqr = direction.sqrMagnitude;
        double distance = System.Math.Sqrt(distanceSqr);

        // Prevent division by zero
        if (distance < 1e-10) return Vector3D.zero;

        // Calculate s/d ratio (cell size / distance)
        double ratio = node.size / distance;

        // If this is a leaf OR the node is far enough away, use approximation
        if (node.IsLeaf || ratio < theta)
        {
            // F = G * m1 * m2 / r^2
            // a = F / m1 = G * m2 / r^2
            double forceMagnitude = StaticVariables.gravitationalConstant * node.totalMass / distanceSqr;
            return direction.normalized * forceMagnitude;
        }

        // Otherwise, recursively calculate for all children
        Vector3D acceleration = Vector3D.zero;
        if (node.children != null)
        {
            foreach (var child in node.children)
            {
                if (child != null && !child.IsEmpty)
                {
                    acceleration += CalculateAccelerationRecursive(child, body);
                }
            }
        }

        return acceleration;
    }

    /// <summary>
    /// Debug visualization of the octree structure
    /// </summary>
    public void DrawGizmos()
    {
        if (root != null)
        {
            DrawNodeGizmos(root);
        }
    }

    private void DrawNodeGizmos(OctreeNode node)
    {
        if (node == null || node.IsEmpty) return;

        // Draw node bounds
        Gizmos.color = node.IsLeaf ? Color.green : new Color(1, 1, 1, 0.1f);
        Gizmos.DrawWireCube(node.center.ToVector3(), Vector3.one * (float)node.size);

        // Draw center of mass
        if (node.totalMass > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(node.centerOfMass.ToVector3(), (float)(node.size * 0.02));
        }

        // Recursively draw children
        if (node.children != null)
        {
            foreach (var child in node.children)
            {
                DrawNodeGizmos(child);
            }
        }
    }
}
