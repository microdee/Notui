﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using md.stdl.Interaction;
using md.stdl.Mathematics;
using VVVV.Utils.VMath;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Notui.Elements
{
    /// <inheritdoc />
    /// <summary>
    /// Base class for elements on a single plane defined by the element transforms
    /// </summary>
    public abstract class PlanarElement : NotuiElement
    {
        /// <inheritdoc cref="NotuiElement"/>
        protected override void MainloopBeforeBehaviors()
        {
            foreach (var touch in Touching.Keys)
            {
                PureHitTest(touch, false, out var persistent);
                Touching[touch] = persistent;
            }
        }

        /// <summary>
        /// General Hittesting on the infinite plane defined by the element transforms
        /// </summary>
        /// <param name="touch">The touch to be tested</param>
        /// <param name="prevpos">Calculate Intersection point for previous position</param>
        /// <returns>If the touch hits then an Intersection point otherwise null</returns>
        public IntersectionPoint PreparePlanarShapeHitTest(Touch touch, bool prevpos)
        {
            // when first hit consider the display transformation then
            // for the rest of the interaction consider the interaction transform
            touch.GetPreviousWorldPosition(Context, out var popos, out var pdir);
            var hit = Intersections.PlaneRay(
                prevpos ? popos : touch.WorldPosition,
                prevpos ? pdir : touch.ViewDir,
                DisplayMatrix,
                out var ispoint,
                out var planarpoint);
            return hit ? new IntersectionPoint(ispoint, planarpoint, this, touch) {SurfaceSpace = planarpoint * 2} : null;
        }

        /// <summary></summary>
        /// <param name="prototype"></param>
        /// <param name="context"></param>
        /// <param name="parent"></param>
        protected PlanarElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent) { }
    }

    /// <inheritdoc />
    /// <summary>
    /// Prototype for empty element. Good for grouping together children without the need for hittesting
    /// </summary>
    public class VoidElementPrototype : ElementPrototype
    {
        /// <summary>
        /// Regular constructor
        /// </summary>
        /// <param name="id">If null generate a new ID with System.GUID</param>
        /// <param name="parent">Optional parent element if this prototype is a child</param>
        public VoidElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(VoidElement), id, parent)
        { }

        /// <summary>
        /// Construct based on an instance
        /// </summary>
        /// <param name="fromInstance">The element instance</param>
        /// <param name="newId">Generate a new ID?</param>
        /// <remarks>Static function ElementPrototype.CreateFromInstance(...) is recommended to be used instead of this</remarks>
        public VoidElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }
    }
    /// <inheritdoc />
    /// <summary>
    /// Empty element. Good for grouping together children without the need for hittesting
    /// </summary>
    public class VoidElement : NotuiElement
    {
        /// <inheritdoc cref="NotuiElement"/>
        public override IntersectionPoint PureHitTest(Touch touch, bool prevpos, out IntersectionPoint persistentIspoint)
        {
            persistentIspoint = null;
            return null;
        }

        /// <inheritdoc cref="NotuiElement"/>
        public VoidElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent)
        { }
    }

    /// <inheritdoc />
    /// <summary>
    /// Infinite planar element prototype. Good for backgrounds or scrolling
    /// </summary>
    public class InfinitePlaneElementPrototype : ElementPrototype
    {
        /// <summary>
        /// Regular constructor
        /// </summary>
        /// <param name="id">If null generate a new ID with System.GUID</param>
        /// <param name="parent">Optional parent element if this prototype is a child</param>
        public InfinitePlaneElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(InfinitePlaneElement), id, parent)
        { }

        /// <summary>
        /// Construct based on an instance
        /// </summary>
        /// <param name="fromInstance">The element instance</param>
        /// <param name="newId">Generate a new ID?</param>
        /// <remarks>Static function ElementPrototype.CreateFromInstance(...) is recommended to be used instead of this</remarks>
        public InfinitePlaneElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }
    }
    /// <inheritdoc />
    /// <summary>
    /// Infinite planar element. Good for backgrounds or scrolling
    /// </summary>
    public class InfinitePlaneElement : PlanarElement
    {
        /// <inheritdoc cref="NotuiElement"/>
        public override IntersectionPoint PureHitTest(Touch touch, bool prevpos, out IntersectionPoint persistentIspoint)
        {
            persistentIspoint = PreparePlanarShapeHitTest(touch, prevpos);
            return persistentIspoint;
        }

        /// <inheritdoc cref="NotuiElement"/>
        public InfinitePlaneElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent)
        { }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar rectangle element prototype
    /// </summary>
    public class RectangleElementPrototype : ElementPrototype
    {
        /// <summary>
        /// Regular constructor
        /// </summary>
        /// <param name="id">If null generate a new ID with System.GUID</param>
        /// <param name="parent">Optional parent element if this prototype is a child</param>
        public RectangleElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(RectangleElement), id, parent) { }

        /// <summary>
        /// Construct based on an instance
        /// </summary>
        /// <param name="fromInstance">The element instance</param>
        /// <param name="newId">Generate a new ID?</param>
        /// <remarks>Static function ElementPrototype.CreateFromInstance(...) is recommended to be used instead of this</remarks>
        public RectangleElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }
    }
    /// <inheritdoc />
    /// <summary>
    /// Planar rectangle element instance
    /// </summary>
    public class RectangleElement : PlanarElement
    {
        /// <inheritdoc cref="NotuiElement"/>
        public override IntersectionPoint PureHitTest(Touch touch, bool prevpos, out IntersectionPoint persistentIspoint)
        {
            var intersection = PreparePlanarShapeHitTest(touch, prevpos);
            var phit = intersection != null;
            if (!phit)
            {
                persistentIspoint = null;
                return null;
            }
            var hit = intersection.ElementSpace.X <= 0.5 && intersection.ElementSpace.X >= -0.5 &&
                      intersection.ElementSpace.Y <= 0.5 && intersection.ElementSpace.Y >= -0.5;
            persistentIspoint = intersection;
            return hit ? intersection : null;
        }

        /// <inheritdoc cref="NotuiElement"/>
        public RectangleElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent) { }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar circle element prototype
    /// </summary>
    public class CircleElementPrototype : ElementPrototype
    {
        /// <summary>
        /// Regular constructor
        /// </summary>
        /// <param name="id">If null generate a new ID with System.GUID</param>
        /// <param name="parent">Optional parent element if this prototype is a child</param>
        public CircleElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(CircleElement), id, parent)
        { }

        /// <summary>
        /// Construct based on an instance
        /// </summary>
        /// <param name="fromInstance">The element instance</param>
        /// <param name="newId">Generate a new ID?</param>
        /// <remarks>Static function ElementPrototype.CreateFromInstance(...) is recommended to be used instead of this</remarks>
        public CircleElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }
    }
    /// <inheritdoc />
    /// <summary>
    /// Planar circle element instance
    /// </summary>
    public class CircleElement : PlanarElement
    {
        /// <inheritdoc cref="NotuiElement"/>
        public override IntersectionPoint PureHitTest(Touch touch, bool prevpos, out IntersectionPoint persistentIspoint)
        {
            var intersection = PreparePlanarShapeHitTest(touch, prevpos);
            var phit = intersection != null;
            if (!phit)
            {
                persistentIspoint = null;
                return null;
            }

            //TODO: Ugly as fuck, fix phase like a human being
            var uvpos = Coordinates.RectToPolar(Vector2.Transform(intersection.ElementSpace.xy(), Matrix3x2.CreateRotation((float)Math.PI)));
            var d = uvpos.Y;
            uvpos.X = uvpos.X / (float)Math.PI;
            uvpos.Y = uvpos.Y * 4 - 1;
            intersection.SurfaceSpace = new Vector3(uvpos, 0);

            var str = Matrix4x4.CreateWorld(intersection.ElementSpace, Vector3.UnitZ,
                -Vector3.Normalize(intersection.ElementSpace));
            intersection.WorldSurfaceTangentTransform = str * DisplayMatrix;

            persistentIspoint = intersection;
            return d < 0.5 ? intersection : null;
        }

        /// <inheritdoc cref="NotuiElement"/>
        public CircleElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent) { }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar circular segment element prototype
    /// </summary>
    public class SegmentElementPrototype : ElementPrototype
    {
        /// <summary>
        /// The radius of the hole in the segment (for creating arcs)
        /// </summary>
        public float HoleRadius { get; set; } = 0;

        /// <summary>
        /// The cycles around the circle perimeter
        /// </summary>
        public float Cycles { get; set; } = 1;

        /// <summary>
        /// The phase of the segment / arc
        /// </summary>
        public float Phase { get; set; } = 0;

        /// <summary>
        /// Regular constructor
        /// </summary>
        /// <param name="id">If null generate a new ID with System.GUID</param>
        /// <param name="parent">Optional parent element if this prototype is a child</param>
        public SegmentElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(SegmentElement), id, parent)
        { }

        /// <summary>
        /// Construct based on an instance
        /// </summary>
        /// <param name="fromInstance">The element instance</param>
        /// <param name="newId">Generate a new ID?</param>
        /// <remarks>Static function ElementPrototype.CreateFromInstance(...) is recommended to be used instead of this</remarks>
        public SegmentElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }
        
        /// <inheritdoc cref="ElementPrototype"/>
        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is SegmentElementPrototype prot)
            {
                HoleRadius = prot.HoleRadius;
                Cycles = prot.Cycles;
                Phase = prot.Phase;
            }
        }

        /// <inheritdoc cref="ElementPrototype"/>
        public override void UpdateFrom(NotuiElement other)
        {
            base.UpdateFrom(other);
            if (other is SegmentElement element)
            {
                HoleRadius = element.HoleRadius;
                Cycles = element.Cycles;
                Phase = element.Phase;
            }
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar circular segment element instance
    /// </summary>
    public class SegmentElement : PlanarElement
    {
        /// <summary>
        /// The radius of the hole in the segment (for creating arcs)
        /// </summary>
        public float HoleRadius { get; set; } = 0;
        /// <summary>
        /// The cycles around the circle perimeter
        /// </summary>
        public float Cycles { get; set; } = 1;
        /// <summary>
        /// The phase of the segment / arc
        /// </summary>
        public float Phase { get; set; } = 0;

        /// <inheritdoc cref="NotuiElement"/>
        public override IntersectionPoint PureHitTest(Touch touch, bool prevpos, out IntersectionPoint persistentIspoint)
        {
            var intersection = PreparePlanarShapeHitTest(touch, prevpos);
            var phit = intersection != null;
            if (!phit)
            {
                persistentIspoint = null;
                return null;
            }
            //var polar = Coordinates.RectToPolar(intersection.ElementSpace.xy());
            //polar.X = (float)Math.PI + (polar.X - Phase * (float)Math.PI*2) * Math.Sign(Cycles);
            var rad = Math.Max(HoleRadius, 1);
            var hrad = Math.Min(HoleRadius, 1);

            var uvpos = Coordinates.RectToPolar(
                Vector2.Transform(
                    intersection.ElementSpace.xy(),
                    Matrix3x2.CreateRotation((-Phase + 0.5f) * 2.0f * (float)Math.PI)
                    )
                );

            uvpos.X = Cycles > 0 ?
                    (float) VMath.Map(uvpos.X / (float) Math.PI, -1, Cycles * 2 - 1, -1, 1, TMapMode.Float) :
                    (float) VMath.Map(uvpos.X / (float) Math.PI, 1, 1 + Cycles * 2, -1, 1, TMapMode.Float);

            uvpos.Y = HoleRadius > 1 ?
                (float)VMath.Map(uvpos.Y * 2, hrad, rad, -1, 1, TMapMode.Float) :
                (float)VMath.Map(uvpos.Y * 2, hrad, rad, 1, -1, TMapMode.Float);
            intersection.SurfaceSpace = new Vector3(uvpos, 0);

            var str = Matrix4x4.CreateWorld(intersection.ElementSpace, Vector3.UnitZ,
                -Vector3.Normalize(intersection.ElementSpace));
            intersection.WorldSurfaceTangentTransform = str * DisplayMatrix;

            //var hit = polar.Y * 2 < rad && polar.Y * 2 >= hrad && (polar.X + Math.PI) % (Math.PI * 2) <= Math.Abs(Cycles * Math.PI * 2);
            var hit = uvpos.X > -1 && uvpos.X < 1 && uvpos.Y > -1 && uvpos.Y < 1;

            persistentIspoint = intersection;
            return hit ? intersection : null;
        }

        /// <inheritdoc cref="NotuiElement"/>
        public SegmentElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent)
        {
            if (prototype is SegmentElementPrototype seprot)
            {
                HoleRadius = seprot.HoleRadius;
                Cycles = seprot.Cycles;
                Phase = seprot.Phase;
            }
        }

        /// <inheritdoc cref="NotuiElement"/>
        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is SegmentElementPrototype prot)
            {
                HoleRadius = prot.HoleRadius;
                Cycles = prot.Cycles;
                Phase = prot.Phase;
            }
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar arbitrary polygon element
    /// </summary>
    public class PolygonElementPrototype : ElementPrototype
    {
        /// <summary>
        /// List of the polygon vertices in 2D
        /// </summary>
        public List<Vector2> Vertices { get; private set; } = new List<Vector2>();

        /// <summary>
        /// Regular constructor
        /// </summary>
        /// <param name="id">If null generate a new ID with System.GUID</param>
        /// <param name="parent">Optional parent element if this prototype is a child</param>
        public PolygonElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(PolygonElement), id, parent)
        { }

        /// <summary>
        /// Construct based on an instance
        /// </summary>
        /// <param name="fromInstance">The element instance</param>
        /// <param name="newId">Generate a new ID?</param>
        /// <remarks>Static function ElementPrototype.CreateFromInstance(...) is recommended to be used instead of this</remarks>
        public PolygonElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }

        /// <inheritdoc cref="ElementPrototype"/>
        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is PolygonElementPrototype prot)
            {
                Vertices = prot.Vertices.ToList();
            }
        }

        /// <inheritdoc cref="ElementPrototype"/>
        public override void UpdateFrom(NotuiElement other)
        {
            base.UpdateFrom(other);
            if (other is PolygonElement element)
            {
                Vertices = element.Vertices.ToList();
            }
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar arbitrary polygon element instance
    /// </summary>
    public class PolygonElement : PlanarElement
    {
        /// <summary>
        /// List of the polygon vertices in 2D
        /// </summary>
        public List<Vector2> Vertices { get; private set; } = new List<Vector2>();

        /// <inheritdoc cref="NotuiElement"/>
        public override IntersectionPoint PureHitTest(Touch touch, bool prevpos, out IntersectionPoint persistentIspoint)
        {
            if (Vertices.Count < 3)
            {
                persistentIspoint = null;
                return null;
            }

            persistentIspoint = PreparePlanarShapeHitTest(touch, prevpos);
            var phit = persistentIspoint != null;
            if (!phit) return null;

            var vold = Vertices[Vertices.Count - 1];
            var vt = persistentIspoint.ElementSpace.xy();
            var hit = false;
            foreach (var vnew in Vertices)
            {
                Vector2 v1, v2;
                if (vnew.X > vold.X)
                {
                    v1 = vold;
                    v2 = vnew;
                }
                else
                {
                    v1 = vnew;
                    v2 = vold;
                }

                if ((vnew.X < vt.X == vt.X <= vold.X) && (vt.Y - v1.Y) * (v2.X - v1.X) < (v2.Y - v1.Y) * (vt.X - v1.X))
                {
                    hit = !hit;
                }
                vold = vnew;
            }
            return hit ? persistentIspoint : null;
        }

        /// <inheritdoc cref="NotuiElement"/>
        public PolygonElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent)
        {
            if (prototype is PolygonElementPrototype prot)
            {
                Vertices = prot.Vertices.ToList();
            }
        }

        /// <inheritdoc cref="NotuiElement"/>
        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is PolygonElementPrototype prot)
            {
                Vertices = prot.Vertices.ToList();
            }
        }
    }
}
