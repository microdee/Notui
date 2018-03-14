﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using md.stdl.Interaction;
using md.stdl.Interfaces;
using md.stdl.Time;

namespace Notui
{
    public class IntersectionPoint
    {
        public Vector3 WorldSpace { get; set; }
        public Vector3 ElementSpace { get; set; }
        public NotuiElement Element { get; set; }
        public Matrix4x4 Transformation => Matrix4x4.CreateTranslation(ElementSpace) * Element.DisplayMatrix;

        public IntersectionPoint(Vector3 wpos, Vector3 epos, NotuiElement element)
        {
            WorldSpace = wpos;
            ElementSpace = epos;
            Element = element;
        }
    }

    public class TouchInteractionEventArgs : EventArgs
    {
        public Touch Touch;
        public IntersectionPoint IntersectionPoint;
    }

    public class ChildrenUpdatedEventArgs : EventArgs
    {
        public IEnumerable<IElementCommon> Elements;
    }

    /// <inheritdoc cref="ElementPrototype" />
    /// <summary>
    /// Simple element base implementing some useful management functions
    /// </summary>
    public abstract class NotuiElement : IElementCommon, ICloneable<NotuiElement>, IUpdateable<ElementPrototype>, IMainlooping
    {
        private Matrix4x4 _interactionMatrix;
        private Matrix4x4 _displayMatrix;
        private bool _onFadedInInvoked;

        public string Name { get; set; }
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public float FadeOutTime { get; set; }
        public float FadeInTime { get; set; }
        public bool Active { get; set; }
        public bool Transparent { get; set; }
        public List<InteractionBehavior> Behaviors { get; set; } = new List<InteractionBehavior>();
        public AttachedValues Value { get; set; }
        public AuxiliaryObject EnvironmentObject { get; set; }

        public ElementTransformation InteractionTransformation { get; set; } = new ElementTransformation();
        public ElementTransformation DisplayTransformation { get; set; } = new ElementTransformation();

        /// <summary>
        /// The context which this element is assigned to.
        /// </summary>
        public NotuiContext Context { get; }

        /// <summary>
        /// The prototype this element was created from.
        /// </summary>
        public ElementPrototype Prototype { get; set; }

        /// <summary>
        /// The element this element inherits its transformation from. Null if this element is directly in a context.
        /// </summary>
        public NotuiElement Parent { get; set; }

        /// <summary>
        /// Are there touches over this element?
        /// </summary>
        public bool Hit { get; set; }

        /// <summary>
        /// Does this element has touches interacting with it?
        /// </summary>
        public bool Touched { get; set; }

        /// <summary>
        /// Element fading from 0 (faded out) to 1 (faded in)
        /// </summary>
        public float ElementFade { get; set; }

        /// <summary>
        /// List of touches interacting with this element which is managed by this element
        /// </summary>
        public ConcurrentDictionary<Touch, IntersectionPoint> Touching { get; set; } =
            new ConcurrentDictionary<Touch, IntersectionPoint>(new TouchEqualityComparer());
        
        /// <summary>
        /// List of touches directly over this element which is managed by this element
        /// </summary>
        public ConcurrentDictionary<Touch, IntersectionPoint> Hitting { get; set; } =
            new ConcurrentDictionary<Touch, IntersectionPoint>(new TouchEqualityComparer());

        /// <summary>
        /// List of touches hovering this element which is managed by the context
        /// </summary>
        public ConcurrentDictionary<Touch, IntersectionPoint> Hovering { get; set; } =
            new ConcurrentDictionary<Touch, IntersectionPoint>(new TouchEqualityComparer());

        /// <summary>
        /// Elements which will inherit the transformation of this element
        /// </summary>
        public Dictionary<string, NotuiElement> Children { get; set; } = new Dictionary<string, NotuiElement>();

        /// <summary>
        /// Requests context to delete this element and its children
        /// </summary>
        public bool DeleteMe { get; set; }

        /// <summary>
        /// The deletion of this element is started
        /// </summary>
        public bool Dying { get; set; }

        /// <summary>
        /// Element age since creation
        /// </summary>
        public StopwatchInteractive Age { get; set; } = new StopwatchInteractive();

        /// <summary>
        /// Start this stopwatch to fade out this element before deletion.
        /// </summary>
        /// <remarks>
        /// Metalocalypse
        /// </remarks>
        public StopwatchInteractive Dethklok { get; set; } = new StopwatchInteractive();

        /// <summary>
        /// Was interaction matrix already calculated since last request.
        /// </summary>
        public bool InteractionMatrixCached { get; set; }

        /// <summary>
        /// Was display matrix already calculated since last request.
        /// </summary>
        public bool DisplayMatrixCached { get; set; }

        /// <summary>
        /// Absolute world interaction transformation.
        /// </summary>
        public Matrix4x4 InteractionMatrix
        {
            get
            {
                if (!InteractionMatrixCached)
                {
                    _interactionMatrix = GetInteractionTransform();
                    InteractionMatrixCached = true;
                }
                return _interactionMatrix;
            }
        }

        /// <summary>
        /// Absolute world display transformation.
        /// </summary>
        public Matrix4x4 DisplayMatrix
        {
            get
            {
                if (!DisplayMatrixCached)
                {
                    _displayMatrix = GetDisplayTransform();
                    DisplayMatrixCached = true;
                }
                return _displayMatrix;
            }
        }

        /// <summary>
        /// Pure function for getting the matrix of the display transformation
        /// </summary>
        /// <returns></returns>
        public Matrix4x4 GetDisplayTransform()
        {
            var parent = Matrix4x4.Identity;
            if (Parent != null) parent = Parent.GetDisplayTransform();
            return DisplayTransformation.Matrix * parent;
        }

        /// <summary>
        /// Pure function for getting the matrix of the interaction transformation
        /// </summary>
        /// <returns></returns>
        public Matrix4x4 GetInteractionTransform()
        {
            var parent = Matrix4x4.Identity;
            if (Parent != null) parent = Parent.GetDisplayTransform();
            return InteractionTransformation.Matrix * parent;
        }

        /// <summary>
        /// Pure function for flattening the element hiararchy into a single list
        /// </summary>
        /// <param name="flatElements">The list containing the result</param>
        public void FlattenElements(List<NotuiElement> flatElements)
        {
            flatElements.Add(this);
            foreach (var child in Children.Values)
            {
                child.FlattenElements(flatElements);
            }
        }

        public void UpdateFromDisplayToInteraction(IElementCommon element)
        {
            InteractionTransformation.UpdateFrom(element.DisplayTransformation);
        }

        public void UpdateFromInteractionToDisplay(IElementCommon element)
        {
            DisplayTransformation.UpdateFrom(element.InteractionTransformation);
        }

        public void FollowDisplay(IElementCommon element)
        {
            DisplayTransformation.UpdateFrom(element.DisplayTransformation);
        }

        public void FollowInteraction(IElementCommon element)
        {
            InteractionTransformation.UpdateFrom(element.InteractionTransformation);
        }

        /// <summary>
        /// Event on the first of multiple touches interacting with this element until the last touch is released
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnInteractionBegin;

        /// <summary>
        /// Event on the last touch interacting with this element is released
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnInteractionEnd;

        /// <summary>
        /// Event fired when a touch started interacting with this element
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnTouchBegin;

        /// <summary>
        /// Event on the release of a touch interacting with this element
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnTouchEnd;

        /// <summary>
        /// Event fired when a touch got over this element
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnHitBegin;

        /// <summary>
        /// Event fired when a touch left this element
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnHitEnd;

        /// <summary>
        /// Event fired on every frame while the element is being interacted with
        /// </summary>
        public event EventHandler OnInteracting;

        /// <summary>
        /// Event fired when a child element is added
        /// </summary>
        public event EventHandler<ChildrenUpdatedEventArgs> OnChildrenUpdated;

        /// <summary>
        /// Event fired when the Dethklok is started. Except when FadeOutTime is set to 0.
        /// </summary>
        public event EventHandler OnDeletionStarted;

        /// <summary>
        /// Event fired when the Element requested its deletion
        /// </summary>
        public event EventHandler OnDeleting;

        /// <summary>
        /// Event fired when the Element finished fading
        /// </summary>
        public event EventHandler OnFadedIn;

        /// <summary>
        /// First thing invoked on the mainloop (even before the virtual MainLoopBegin)
        /// </summary>
        public event EventHandler OnMainLoopBegin;

        /// <summary>
        /// Last thing invoked on the mainloop (even after the virtual MainLoopEnd)
        /// </summary>
        public event EventHandler OnMainLoopEnd;

        /// <summary>
        /// Add or update children of this element from prototypes
        /// </summary>
        /// <param name="children">children to be added</param>
        /// <param name="removeNotPresent">Remove Children from elements not present in the input</param>
        /// <returns>New children added to the element</returns>
        public List<NotuiElement> UpdateChildren(bool removeNotPresent = false, params ElementPrototype[] children)
        {
            var newchildren = new List<NotuiElement>();
            if (removeNotPresent)
            {
                var removablechildren = (from child in Children.Values where children.All(c => c.Id != child.Id) select child).ToArray();
                foreach (var child in removablechildren)
                {
                    child.StartDeletion();
                }
            }

            foreach (var child in children)
            {
                if(Children.ContainsKey(child.Id))
                    Children[child.Id].UpdateFrom(child);
                else if(child.Id != Id)
                {
                    var childinst = child.Instantiate(Context, this);
                    Children.Add(child.Id, childinst);
                    newchildren.Add(childinst);
                }
            }

            OnChildrenUpdated?.Invoke(this, new ChildrenUpdatedEventArgs {Elements = children});
            return newchildren;
        }

        /// <summary>
        /// Pure hittest function used by the context
        /// </summary>
        /// <param name="touch">Current touch</param>
        /// <returns>Return null when the element is not hit by the touch and return the intersection coordinates otherwise</returns>
        public abstract IntersectionPoint HitTest(Touch touch);

        /// <summary>
        /// Used for managing side effects of touch interaction
        /// </summary>
        /// <param name="touch">Current touch</param>
        public virtual void ProcessTouch(Touch touch)
        {
            var hit = Hovering.ContainsKey(touch);
            var eventargs = new TouchInteractionEventArgs
            { 
                Touch = touch,
                IntersectionPoint = hit ? Hovering[touch] : null
            };
            if (hit && Touching.ContainsKey(touch)) Touching[touch] = Hovering[touch];
            if (!hit)
            {
                if (!Hitting.ContainsKey(touch)) return;
                Hitting.TryRemove(touch, out var dummy);
                OnHitEnd?.Invoke(this, eventargs);
                if (Touching.ContainsKey(touch)) Touching[touch] = null;
                return;
            }
            if (!Hitting.ContainsKey(touch))
            {
                OnHitBegin?.Invoke(this, eventargs);
                Hitting.TryAdd(touch, Hovering[touch]);
            }
            FireInteractionTouchBegin(touch);
        }

        /// <summary>
        /// Method invoked before anything happens in mainloop, but after OnMainloopBegin event
        /// </summary>
        protected virtual void MainloopBegin() { }
        /// <summary>
        /// Method invoked before behaviors are executed and OnInteracting is invoked
        /// </summary>
        protected virtual void MainloopBeforeBehaviors() { }
        /// <summary>
        /// Method invoked at the end of mainloop
        /// </summary>
        protected virtual void MainloopEnd() { }

        /// <summary>
        /// This is called every frame by the context
        /// </summary>
        /// <remarks>
        /// The context call this function of all flattened elements in parallel regardless of the element hierarchy, you should take this into account when overriding this function or developing behaviors. You MUST NOT call the Mainloop method of the children elements yourself because the context already does so (unless you are really desperate).
        /// </remarks>
        public void Mainloop(float deltatime)
        {
            OnMainLoopBegin?.Invoke(this, EventArgs.Empty);

            InteractionTransformation.SubscribeToChange(Id, transformation => InvalidateMatrices());
            DisplayTransformation.SubscribeToChange(Id, transformation => InvalidateMatrices());

            MainloopBegin();

            var endtouches = ( from touch in Touching.Keys
                where touch.ExpireFrames > Context.ConsiderReleasedAfter || !touch.Pressed
                select touch
                ).ToArray();

            foreach (var touch in endtouches)
                FireTouchEnd(touch);

            var endhits = ( from touch in Hitting.Keys
                where touch.ExpireFrames > Context.ConsiderReleasedAfter
                select touch
                ).ToArray();

            foreach (var touch in endhits)
            {
                var eventargs = new TouchInteractionEventArgs
                {
                    Touch = touch,
                    IntersectionPoint = Hitting[touch]
                };
                Hitting.TryRemove(touch, out var dummy);
                OnHitEnd?.Invoke(this, eventargs);
            }

            Hit = Hitting.Count > 0;
            Touched = Touching.Count > 0;

            foreach (var touch in Hitting.Keys)
            {
                var inters = HitTest(touch);
                Hitting[touch] = inters;
                if (Touching.ContainsKey(touch))
                    Touching[touch] = inters;
            }

            if (FadeInTime > 0)
            {
                ElementFade = Math.Min(Math.Max(0, (float) Age.Elapsed.TotalSeconds / FadeInTime), 1);
            }
            else
            {
                if(ElementFade < 1) OnFadedIn?.Invoke(this, EventArgs.Empty);
                _onFadedInInvoked = true;
                ElementFade = 1;
            }
            if (Age.Elapsed.TotalSeconds >= FadeInTime && !_onFadedInInvoked)
            {
                OnFadedIn?.Invoke(this, EventArgs.Empty);
                _onFadedInInvoked = true;
            }

            if (FadeOutTime > 0)
            {
                ElementFade *= Math.Min(Math.Max(0, 1 - (float)Dethklok.Elapsed.TotalSeconds / FadeOutTime), 1);
                if (Dethklok.Elapsed.TotalSeconds > FadeOutTime && Dethklok.IsRunning && Age.Elapsed.TotalSeconds > Context.DeltaTime)
                {
                    ElementFade = 0;
                    if(!DeleteMe) OnDeleting?.Invoke(this, EventArgs.Empty);
                    DeleteMe = true;
                }
            }

            MainloopBeforeBehaviors();
            if(Touched) OnInteracting?.Invoke(this, EventArgs.Empty);
            foreach (var behavior in Behaviors)
            {
                behavior.Behave(this);
            }

            MainloopEnd();
            OnMainLoopEnd?.Invoke(this, EventArgs.Empty);
        }

        public virtual NotuiElement Copy()
        {
            var newprot = new ElementPrototype(this);
            var newinst = Parent == null ?
                Context.AddOrUpdateElements(false, newprot)[0] :
                Parent.UpdateChildren(false, newprot)[0];

            return newinst;
        }

        /// <summary>
        /// Notify the need for recomputing absolute world matrices
        /// </summary>
        public void InvalidateMatrices()
        {
            InteractionMatrixCached = false;
            DisplayMatrixCached = false;
            foreach (var child in Children.Values)
                child.InvalidateMatrices();
        }

        public void UpdateFrom(ElementPrototype other)
        {
            this.UpdateCommon(other, other.TransformApplication);
            Value?.UpdateFrom(other.Value);
            UpdateChildren(true, other.Children.Values.ToArray());
        }

        /// <summary>
        /// Base constructor awaiting an element prototype, a context to create element into and an optional parent element
        /// </summary>
        /// <param name="prototype"></param>
        /// <param name="context"></param>
        /// <param name="parent"></param>
        protected NotuiElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null)
        {
            this.UpdateCommon(prototype, ApplyTransformMode.All);
            Value = prototype.Value?.Copy();
            Context = context;
            Parent = parent;

            EnvironmentObject = prototype.EnvironmentObject?.Copy();

            foreach (var child in prototype.Children.Values)
            {
                var newchild = child.Instantiate(context, this);
                Children.Add(child.Id, newchild);
            }
            Age.Start();
        }

        protected void FireInteractionTouchBegin(Touch touch)
        {
            if(touch.FramesSincePressed >= Context.ConsiderNewBefore) return;
            if(Touching.ContainsKey(touch)) return;
            if(!touch.Pressed) return;

            var eventargs = new TouchInteractionEventArgs { Touch = touch };
            if (Touching.Count == 0) OnInteractionBegin?.Invoke(this, eventargs);
            OnTouchBegin?.Invoke(this, eventargs);
            Touching.TryAdd(touch, Hovering[touch]);
        }
        protected void FireInteractionEnd(Touch touch)
        {
            if (Touching.Count == 0) OnInteractionEnd?.Invoke(this, new TouchInteractionEventArgs
            {
                Touch = touch
            });
        }
        protected void FireTouchEnd(Touch touch)
        {
            if (!Touching.ContainsKey(touch)) return;
            Touching.TryRemove(touch, out var dummy);
            OnTouchEnd?.Invoke(this, new TouchInteractionEventArgs
            {
                Touch = touch
            });
            FireInteractionEnd(touch);
        }

        public object Clone() => Copy();

        /// <summary>
        /// Start the Dethklok or if FadeOutTime is 0 just set DeleteMe true and invoke OnDeleting
        /// </summary>
        public void StartDeletion()
        {
            Dying = true;
            foreach (var child in Children.Values)
            {
                child.StartDeletion();
            }
            if (FadeOutTime > 0)
            {
                if(!Dethklok.IsRunning) OnDeletionStarted?.Invoke(this, EventArgs.Empty);
                Dethklok.Start();
            }
            else
            {
                ElementFade = 0;
                if(Age.Elapsed.TotalSeconds > Context.DeltaTime) OnDeleting?.Invoke(this, EventArgs.Empty);
                DeleteMe = true;
            }
        }
    }
}

