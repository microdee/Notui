﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using md.stdl.Coding;
using md.stdl.Interaction;
using md.stdl.Interfaces;
using md.stdl.Mathematics;
using VVVV.Utils.IO;
using VVVV.Utils.VMath;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Notui
{
    /// <inheritdoc cref="IMainlooping"/>
    /// <summary>
    /// Notui Context to manage GuiElements and Touches
    /// </summary>
    public class NotuiContext : IMainlooping
    {
        /// <summary>
        /// Use PLINQ or not?
        /// </summary>
        public bool UseParallel { get; set; } = true;
        /// <summary>
        /// Execute from Parent to child or on the FlatElements
        /// </summary>
        public bool MaintainExecutionOrder { get; set; } = true;

        /// <summary>
        /// Consider touches to be new before the age of this amount of frames
        /// </summary>
        public int ConsiderNewBefore { get; set; } = 1;
        /// <summary>
        /// Ignore and delete touches older than this amount of frames
        /// </summary>
        public int ConsiderReleasedAfter { get; set; } = 1;
        /// <summary>
        /// To consider a touch minimum this amount of force have to be applied
        /// </summary>
        public float MinimumForce { get; set; } = 0.5f;

        /// <summary>
        /// Mouse will omit a touch with this ID
        /// </summary>
        public int MouseTouchId { get; set; } = -1;
        /// <summary>
        /// Assign this force to the touch generated by the mouse
        /// </summary>
        public float MouseTouchForce { get; set; } = 1.0f;
        /// <summary>
        /// If true the mouse will generate a touch every frame
        /// </summary>
        public bool MouseAlwaysPresent { get; set; } = true;
        /// <summary>
        /// Mouse object which will generate a touch with ID of -1 and touch force of MouseTouchForce. If a mouse is not submitted this stays null
        /// </summary>
        public Mouse AttachableMouse { get; private set; }
        /// <summary>
        /// If a Mouse is submitted this property stores the mouse delta, otherwise null
        /// </summary>
        public AccumulatingMouseObserver MouseDelta { get; private set; }

        /// <summary>
        /// Optional camera view matrix
        /// </summary>
        public Matrix4x4 View { get; set; } = Matrix4x4.Identity;
        /// <summary>
        /// Optional camera projection matrix
        /// </summary>
        public Matrix4x4 Projection { get; set; } = Matrix4x4.Identity;
        /// <summary>
        /// Very optional aspect ratio correction matrix a'la vvvv
        /// </summary>
        public Matrix4x4 AspectRatio { get; set; } = Matrix4x4.Identity;
        
        /// <summary>
        /// Inverse of view transform
        /// </summary>
        public Matrix4x4 ViewInverse { get; private set; } = Matrix4x4.Identity;
        /// <summary>
        /// Inverse of projection transform combined with aspect ratio
        /// </summary>
        public Matrix4x4 ProjectionWithAspectRatioInverse { get; private set; } = Matrix4x4.Identity;
        /// <summary>
        /// Projection transform combined with aspect ratio
        /// </summary>
        public Matrix4x4 ProjectionWithAspectRatio { get; private set; } = Matrix4x4.Identity;
        /// <summary>
        /// Camera Position in world
        /// </summary>
        public Vector3 ViewPosition { get; private set; } = Vector3.Zero;
        /// <summary>
        /// Camera view direction in world
        /// </summary>
        public Vector3 ViewDirection { get; private set; } = Vector3.UnitZ;
        /// <summary>
        /// Camera view orientation in world
        /// </summary>
        public Quaternion ViewOrientation { get; private set; } = Quaternion.Identity;
        /// <summary>
        /// Delta time between mainloop calls in seconds
        /// </summary>
        /// <remarks>
        /// This is provided by the implementer in the Mainloop args
        /// </remarks>
        public float DeltaTime { get; private set; } = 0;

        /// <summary>
        /// All the touches in this context
        /// </summary>
        public ConcurrentDictionary<int, Touch> Touches { get; } =
            new ConcurrentDictionary<int, Touch>();

        /// <summary>
        /// Elements in this context without a parent (or Root elements)
        /// </summary>
        public Dictionary<string, NotuiElement> RootElements { get; } = new Dictionary<string, NotuiElement>();

        /// <summary>
        /// All the elements in this context including the children of the root elements recursively
        /// </summary>
        public List<NotuiElement> FlatElements { get; } = new List<NotuiElement>();
        
        private readonly List<(Vector2 point, int id, float force)> _inputTouches = new List<(Vector2 point, int id, float force)>();

        private IDisposable _mouseUnsubscriber;
        private Vector2 _mouseTouchPos;
        private bool _rebuild = false;

        public event EventHandler OnMainLoopBegin;
        public event EventHandler OnMainLoopEnd;

        /// <summary>
        /// Call this function every frame before Context.Mainloop
        /// </summary>
        /// <param name="touches">List of touch primitives to work with</param>
        public void SubmitTouches(IEnumerable<(Vector2, int, float)> touches)
        {
            _inputTouches.Clear();
            _inputTouches.AddRange(touches);
        }

        /// <summary>
        /// Use a mouse to interact with elements and generate touches from it
        /// </summary>
        /// <param name="mouse">The selected mouse device</param>
        public void SubmitMouse(Mouse mouse)
        {
            MouseDelta?.Unsubscribe();
            _mouseUnsubscriber?.Dispose();

            AttachableMouse = mouse;
            _mouseUnsubscriber = mouse.MouseNotifications.Subscribe(mn =>
            {
                if (mn.Kind == MouseNotificationKind.MouseMove)
                    _mouseTouchPos = mn.Position.FromMousePoint(mn.ClientArea).AsSystemVector();
            });
            MouseDelta = new AccumulatingMouseObserver();
            MouseDelta.SubscribeTo(mouse.MouseNotifications);
        }

        /// <summary>
        /// No longer generate touches from the submitted mouse
        /// </summary>
        public void DetachMouse()
        {
            if(MouseDelta == null || AttachableMouse == null) return;
            MouseDelta.Unsubscribe();
            _mouseUnsubscriber.Dispose();

            MouseDelta = null;
            AttachableMouse = null;
            _mouseUnsubscriber = null;
        }

        public void RequestRebuild(bool deleted, bool updated)
        {
            _rebuild = true;
            if (deleted) _elementsDeleted = true;
            if (updated) _elementsUpdated = true;
        }
        
        public void Mainloop(float deltatime)
        {
            OnMainLoopBegin?.Invoke(this, EventArgs.Empty);

            Matrix4x4.Invert(AspectRatio, out var invasp);
            //Matrix4x4.Invert(Projection, out var invproj);
            Matrix4x4.Invert(View, out var invview);
            var aspproj = Projection * invasp;
            Matrix4x4.Invert(aspproj, out var invaspproj);

            ViewInverse = invview;
            ProjectionWithAspectRatio = aspproj;
            ProjectionWithAspectRatioInverse = invaspproj;
            DeltaTime = deltatime;

            Matrix4x4.Decompose(invview, out var vscale, out var vquat, out var vpos);
            ViewOrientation = vquat;
            ViewPosition = vpos;
            ViewDirection = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, View));

            // Removing expired touches
            var removabletouches = (from touch in Touches.Values
                where touch.ExpireFrames > ConsiderReleasedAfter
                select touch.Id).ToArray();
            foreach (var tid in removabletouches)
            {
                Touches.TryRemove(tid, out var dummy);
            }

            foreach (var touch in Touches.Values)
            {
                touch.Mainloop(deltatime);
                touch.HittingElements.Clear();
            }

            // Scan through elements if any of them wants to be killed or if there are new ones
            foreach (var element in FlatElements)
            {
                var dethklok = element.Dethklok;
                var deleteme = element.DeleteMe || (float)dethklok.Elapsed.TotalSeconds > element.FadeOutTime && element.Dying;
                if (!deleteme) continue;
                _rebuild = true;
                _elementsDeleted = true;
                if (element.Parent == null) RootElements.Remove(element.Id);
                else element.Parent.Children.Remove(element.Id);
            }

            if (_elementsDeleted)
            {
                _elementsDeleted = false;
                OnElementsDeleted?.Invoke(this, EventArgs.Empty);
            }
            if (_elementsUpdated)
            {
                _rebuild = true;
                _elementsUpdated = false;
                OnElementsUpdated?.Invoke(this, EventArgs.Empty);
            }
            if(_rebuild) BuildFlatList();
            _rebuild = false;

            // Process input touches
            foreach (var touch in _inputTouches)
            {
                Touch tt;
                if (Touches.ContainsKey(touch.id))
                {
                    tt = Touches[touch.id];
                }
                else
                {
                    tt = new Touch(touch.id, this) { Force = touch.force };
                    Touches.TryAdd(tt.Id, tt);
                }
                tt.Update(touch.point, deltatime);
                tt.Force = touch.force;
                tt.Press(MinimumForce);
            }
            if (MouseDelta != null && AttachableMouse != null && _inputTouches.Count == 0)
            {
                var mbpressed = MouseDelta.MouseClicks.Values.Any(mc => mc.Pressed);
                if (MouseAlwaysPresent || mbpressed)
                {
                    Touch tt;
                    if (Touches.ContainsKey(MouseTouchId))
                    {
                        tt = Touches[MouseTouchId];
                    }
                    else
                    {
                        tt = new Touch(MouseTouchId, this) { Force = mbpressed ? MouseTouchForce : 0.0f };
                        tt.AttachMouse(AttachableMouse, MouseDelta);
                        Touches.TryAdd(tt.Id, tt);
                    }
                    tt.Update(_mouseTouchPos, deltatime);
                    tt.Force = mbpressed ? MouseTouchForce : 0.0f;
                    tt.Press(MinimumForce);
                }
            }


            // preparing elements for hittest
            foreach (var element in FlatElements)
            {
                element.Hovering.Clear();
            }

            // look at which touches hit which element
            void ProcessTouches(Touch touch)
            {
                // Transform touches into world
                Coordinates.GetPointWorldPosDir(touch.Point, invaspproj, invview, out var tpw, out var tpd);
                touch.WorldPosition = tpw;
                touch.ViewDir = tpd;

                // get hitting intersections and order them from closest to furthest
                var intersections = FlatElements.Select(el =>
                    {
                        var intersection = el.HitTest(touch);
                        //if (intersection != null) intersection.Element = el;
                        return intersection;
                    })
                    .Where(insec => insec != null)
                    .Where(insec => insec.Element.Active)
                    .OrderBy(insec =>
                    {
                        var screenpos = Vector4.Transform(new Vector4(insec.WorldTransform.Translation, 1), View * aspproj);
                        return screenpos.Z / screenpos.W;
                    });

                // Sift through ordered intersection list until the furthest non-transparent element
                // or in other words ignore all intersected elements which are further away from the closest non-transparent element
                var passedintersections = GetTopInteractableElements(intersections);

                // Add the touch and the corresponding intersection point to the interacting elements
                // and attach those elements to the touch too.
                touch.HittingElements.AddRange(passedintersections.Select(insec =>
                {
                    insec.Element.Hovering.TryAdd(touch, insec);
                    return insec.Element;
                }));

            }
            if(UseParallel) Touches.Values.AsParallel().ForAll(ProcessTouches);
            else Touches.Values.ForEach(ProcessTouches);

            // Do element logic
            if(MaintainExecutionOrder) HierarchicalExecution();
            else FlatExecution();

            MouseDelta?.Mainloop(deltatime);
            OnMainLoopEnd?.Invoke(this, EventArgs.Empty);
        }

        private void ProcessElements(NotuiElement el)
        {
            foreach (var touch in Touches.Values)
            {
                el.ProcessTouch(touch);
            }
            el.Mainloop(DeltaTime);
        }

        private void FlatExecution()
        {
            if (UseParallel) FlatElements.AsParallel().ForAll(ProcessElements);
            else FlatElements.ForEach(ProcessElements);
        }

        private void HierarchicalExecution()
        {
            void RecursiveChildrenExec(NotuiElement recel)
            {
                ProcessElements(recel);
                if (recel.Children.Count <= 0) return;
                //if (UseParallel) recel.Children.Values.AsParallel().ForAll(RecursiveChildrenExec);
                /*else*/ recel.Children.Values.ForEach(RecursiveChildrenExec);
            }

            if (UseParallel) RootElements.Values.AsParallel().ForAll(RecursiveChildrenExec);
            else RootElements.Values.ForEach(RecursiveChildrenExec);
        }

        /// <summary>
        /// Instantiate new elements and update existing elements from the input prototypes. Optionally start the deletion of elements which are not present in the input array.
        /// </summary>
        /// <param name="removeNotPresent">When true elements will be deleted if their prototype with the same ID is not found in the input array</param>
        /// <param name="elements">Input prototypes</param>
        /// <returns>List of the newly instantiated elements</returns>
        public List<NotuiElement> AddOrUpdateElements(bool removeNotPresent, params ElementPrototype[] elements)
        {
            var newelements = new List<NotuiElement>();
            if (removeNotPresent)
            {
                var removables = (from el in RootElements.Values where elements.All(c => c.Id != el.Id) select el).ToArray();
                foreach (var el in removables)
                {
                    el.StartDeletion();
                }
            }

            foreach (var el in elements)
            {
                if (RootElements.ContainsKey(el.Id))
                    RootElements[el.Id].UpdateFrom(el);
                else
                {
                    var elinst = el.Instantiate(this);
                    RootElements.Add(el.Id, elinst);
                    newelements.Add(elinst);
                }
            }
            _elementsUpdated = true;
            return newelements;
        }

        private void BuildFlatList()
        {
            foreach (var element in FlatElements)
            {
                element.OnChildrenUpdated -= OnIndividualElementUpdate;
            }
            FlatElements.Clear();
            foreach (var element in RootElements.Values)
            {
                element.FlattenElements(FlatElements);
            }

            foreach (var element in FlatElements)
            {
                element.OnChildrenUpdated += OnIndividualElementUpdate;
            }
        }

        private bool _elementsDeleted;
        private bool _elementsUpdated;

        private void OnIndividualElementUpdate(object sender, ChildrenUpdatedEventArgs childrenAddedEventArgs)
        {
            _elementsUpdated = true;
        }

        private static IEnumerable<IntersectionPoint> GetTopInteractableElements(IEnumerable<IntersectionPoint> orderedhitinsecs)
        {
            if (orderedhitinsecs == null) yield break;

            foreach (var insec in orderedhitinsecs)
            {
                yield return insec;
                if (insec.Element.Transparent) continue;
                yield break;
            }
        }

        /// <summary>
        /// Fired when elements added or updated
        /// </summary>
        public event EventHandler OnElementsUpdated;

        /// <summary>
        /// Fired when elements got deleted
        /// </summary>
        public event EventHandler OnElementsDeleted;

        /// <summary>
        /// Get elements with Opaq (from RootElements)
        /// </summary>
        /// <param name="path">Opaq path</param>
        /// <param name="separator"></param>
        /// <param name="usename">If true it will use Element names, otherwise their ID</param>
        /// <returns></returns>
        public List<NotuiElement> Opaq(string path, string separator = "/", bool usename = true)
        {
            IEnumerable<NotuiElement> GetChildren(NotuiContext context, string k)
            {
                if (context.RootElements.Count == 0) return Enumerable.Empty<NotuiElement>();
                if (usename) return context.RootElements.Values.Where(c => c.Name == k);
                if (context.RootElements.ContainsKey(k)) return new[] { context.RootElements[k] };
                return Enumerable.Empty<NotuiElement>();
            }
            
            var children = new List<NotuiElement>();
            var nextpath = this.OpaqNonRecursive(path, children, children, separator,
                context => usename ? RootElements.Values.Select(el => el.Name) : RootElements.Keys,
                context => usename ? RootElements.Values.Select(el => el.Name) : RootElements.Keys,
                GetChildren, GetChildren
            );

            if (string.IsNullOrWhiteSpace(nextpath))
                return children;

            var results = new List<NotuiElement>();

            foreach (var child in children)
                results.AddRange(child.Opaq(nextpath, separator, usename));

            return results;
        }
    }
}
