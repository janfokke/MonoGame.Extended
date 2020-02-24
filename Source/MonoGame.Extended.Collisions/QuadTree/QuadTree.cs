using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGame.Extended.Collisions
{
    /// <summary>
    ///     Class for doing collision handling with a quad tree.
    /// </summary>
    public class Quadtree
    {
        public const int DefaultMaxDepth = 7;
        public const int DefaultMaxObjectsPerNode = 25;

        protected List<Quadtree> Children = new List<Quadtree>(4);
        protected HashSet<QuadtreeData> Contents = new HashSet<QuadtreeData>();

        /// <summary>
        ///     Creates a quad tree with the given bounds.
        /// </summary>
        /// <param name="bounds">The bounds of the new quad tree.</param>
        public Quadtree(RectangleF bounds)
        {
            CurrentDepth = 0;
            NodeBounds = bounds;
        }

        protected int CurrentDepth { get; set; }
        protected int MaxDepth { get; set; } = DefaultMaxDepth;

        protected int MaxObjectsPerNode { get; set; } = DefaultMaxObjectsPerNode;

        /// <summary>
        ///     Gets the bounds of the collisionActor contained in this quad tree.
        /// </summary>
        public  RectangleF NodeBounds { get; protected set; }

        /// <summary>
        ///     Gets whether the current node is a leaf node.
        /// </summary>
        public bool IsLeaf => Children.Count == 0;

        /// <summary>
        ///     Counts the number of unique targets in the current Quadtree.
        /// </summary>
        /// <returns>Returns the targets of objects found.</returns>
        public int NumTargets()
        {
            List<QuadtreeData> dirtyItems = new List<QuadtreeData>();
            var objectCount = 0;

            // Do BFS on nodes to count children.
            var process = new Queue<Quadtree>();
            process.Enqueue(this);
            while (process.Count > 0)
            {
                var processing = process.Dequeue();
                if (!processing.IsLeaf)
                {
                    foreach (var child in processing.Children)
                    {
                        process.Enqueue(child);
                    }
                }
                else
                {
                    foreach (var data in processing.Contents)
                    {
                        if (data.Dirty == false)
                        {
                            objectCount++;
                            data.MarkDirty();
                            dirtyItems.Add(data);
                        }
                    }
                }
            }
            for (var i = 0; i < dirtyItems.Count; i++)
            {
                dirtyItems[i].MarkClean();
            }
            return objectCount;
        }

        /// <summary>
        ///     Inserts the data into the tree.
        /// </summary>
        /// <param name="data">Data being inserted.</param>
        public void Insert(QuadtreeData data)
        {
            // Object doesn't fit into this node.
            if (!NodeBounds.Intersects(data.Target.Bounds))
            {
                return;
            }

            if (IsLeaf && Contents.Count >= MaxObjectsPerNode)
            {
                Split();
            }

            if (IsLeaf)
            {
                AddToLeaf(data);
            }
            else
            {
                for (var index = 0; index < 4; index++)
                {
                    var child = Children[index];
                    child.Insert(data);
                }
            }
        }

        public int LayerMask;

        public void UpdateLayerMask()
        {
            LayerMask = 0;
            foreach (QuadtreeData quadtreeData in Contents)
            {
                LayerMask |= quadtreeData.CollisionLayerFlags;
            }
        }

        /// <summary>
        ///     Removes data from the Quadtree
        /// </summary>
        /// <param name="data">The data to be removed.</param>
        public void Remove(QuadtreeData data)
        {
            if (IsLeaf)
            {
                Contents.Remove(data);
                UpdateLayerMask();
            }
            else
            {
                throw new InvalidOperationException($"Cannot remove from a non leaf {nameof(Quadtree)}"); 
            }
        }

        /// <summary>
        ///     Removes unneccesary leaf nodes and simplifies the quad tree.
        /// </summary>
        public void Shake()
        {
            if (!IsLeaf)
            {
                var numObjects = NumTargets();
                if (numObjects == 0)
                {
                    Children.Clear();
                }
                else if (numObjects < MaxObjectsPerNode)
                {
                    List<QuadtreeData> dirtyItems = new List<QuadtreeData>();
                    var process = new Queue<Quadtree>();
                    process.Enqueue(this);
                    while (process.Count > 0)
                    {
                        var processing = process.Dequeue();
                        if (!processing.IsLeaf)
                        {
                            foreach (var subTree in processing.Children)
                            {
                                process.Enqueue(subTree);
                            }
                        }
                        else
                        {
                            foreach (var data in processing.Contents)
                            {
                                data.Parents.Remove(processing);
                                if (data.Dirty == false)
                                {
                                    AddToLeaf(data);
                                    data.MarkDirty();
                                    dirtyItems.Add(data);
                                }
                            }
                        }
                    }
                    for (var i = 0; i < dirtyItems.Count; i++)
                    {
                        dirtyItems[i].MarkClean();
                    }
                    Children.Clear();
                }
                else
                {
                    for (var index = 0; index < Children.Count; index++)
                    {
                        Quadtree quadtree = Children[index];
                        quadtree.Shake();
                    }
                }
            }
        }

        private void AddToLeaf(QuadtreeData data)
        {
            data.AddParent(this);
            Contents.Add(data);
            LayerMask |= data.CollisionLayerFlags;
        }

        /// <summary>
        ///     Splits a quadtree into quadrants.
        /// </summary>
        public void Split()
        {
            if (CurrentDepth + 1 >= MaxDepth) 
                return;

            var min = NodeBounds.TopLeft;
            var max = NodeBounds.BottomRight;
            var center = NodeBounds.Center;

            RectangleF[] childAreas =
            {
                RectangleF.CreateFrom(min, center),
                RectangleF.CreateFrom(new Point2(center.X, min.Y), new Point2(max.X, center.Y)),
                RectangleF.CreateFrom(center, max),
                RectangleF.CreateFrom(new Point2(min.X, center.Y), new Point2(center.X, max.Y))
            };
            
            for (var i = 0; i < childAreas.Length; ++i)
            {
                var node = new Quadtree(childAreas[i]);
                Children.Add(node);
                Children[i].CurrentDepth = CurrentDepth + 1;
            }

            foreach (QuadtreeData contentQuadtree in Contents)
            {
                contentQuadtree.RemoveParent(this);
                for (var index = 0; index < 4; index++)
                {
                    Quadtree childQuadtree = Children[index];
                    childQuadtree.Insert(contentQuadtree);
                }
            }
            Contents.Clear();
        }

        /// <summary>
        ///     Queries the quadtree for targets that intersect with the given captureField.
        /// </summary>
        /// <param name="area">The collisionActor to query for overlapping targets</param>
        /// <returns>A unique list of targets intersected by collisionActor.</returns>
        internal void QueryWithoutReset(RectangleF captureField, List<QuadtreeData> recursiveResult)
        {
            if (!Shape.Intersects(NodeBounds, captureField))
                return;

            if (IsLeaf)
            {
                foreach (QuadtreeData quadtreeData in Contents)
                {
                    if (quadtreeData.Dirty == false && quadtreeData.Bounds.Intersects(captureField))
                    {
                        recursiveResult.Add(quadtreeData);
                        quadtreeData.MarkDirty();
                    }
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    Children[i].QueryWithoutReset(captureField, recursiveResult);
                }
            }
        }

        internal void QueryWithoutReset(QuadtreeData collisionActor, List<QuadtreeData> recursiveResult)
        {
            if(collisionActor.CollisionMaskFlags == 0 || !Shape.Intersects(NodeBounds, collisionActor.Target.Bounds))
                return;

            if (IsLeaf)
            {
                // Check if this quad contains items with target layer
                if((collisionActor.CollisionMaskFlags & LayerMask) == 0)
                    return;

                foreach (QuadtreeData quadtreeData in Contents)
                {
                    if (quadtreeData.Dirty == false
                        && (collisionActor.CollisionMaskFlags & quadtreeData.CollisionLayerFlags) != 0
                        && quadtreeData.Bounds.Intersects(collisionActor.Target.Bounds))
                    {
                        recursiveResult.Add(quadtreeData);
                        quadtreeData.MarkDirty();
                    }
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    Children[i].QueryWithoutReset(collisionActor, recursiveResult);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont spriteFont)
        {
            spriteBatch.DrawString(spriteFont, NumTargets().ToString() ,NodeBounds.Center, Color.Blue);
            spriteBatch.DrawRectangle(NodeBounds, Color.Red);
            if (IsLeaf)
                return;
            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].Draw(spriteBatch,spriteFont);
            }
        }
    }
}