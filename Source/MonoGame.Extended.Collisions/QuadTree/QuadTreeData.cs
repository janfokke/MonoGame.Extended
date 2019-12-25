using System.Collections.Generic;

namespace MonoGame.Extended.Collisions
{
    /// <summary>
    ///     Data structure for the quad tree.
    ///     Holds the entity and collision data for it.
    /// </summary>
    public class QuadtreeData
    {
        public QuadtreeData(ICollisionActor target)
        {
            Target = target;
            Bounds = target.Bounds;
            Flag = false;
        }


        private IShapeF _previous;

        public HashSet<Quadtree> _parents = new HashSet<Quadtree>();

        /// <summary>
        ///     Gets or sets the Target for collision.
        /// </summary>
        public ICollisionActor Target { get; set; }

        /// <summary>
        ///     Gets or sets whether Target has had its collision handled this
        ///     iteration.
        /// </summary>
        public bool Flag { get; set; }

        /// <summary>
        ///     Gets or sets the bounding box for collision detection.
        /// </summary>
        public IShapeF Bounds { get; set; }

        public void AddParent(Quadtree parent)
        {
            _parents.Add(parent);
        }

        public void RemoveFromParents()
        {
            foreach (Quadtree parent in _parents)
            {
                parent.Remove(this);
            }
            _parents.Clear();
        }
    }
}