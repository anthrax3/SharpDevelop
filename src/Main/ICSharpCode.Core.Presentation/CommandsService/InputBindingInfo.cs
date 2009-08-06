using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Reflection;
using CommandManager = System.Windows.Input.CommandManager;
using SDCommandManager = ICSharpCode.Core.Presentation.CommandManager;

namespace ICSharpCode.Core.Presentation
{
	/// <summary>
	/// Stores details about input binding
	/// </summary>
	public class InputBindingInfo : BindingInfoBase
	{
		InputBindingCollection oldBindingCollection = new InputBindingCollection();
		private List<UIElement> oldInstances;
		List<Type> oldTypes;
		InputBindingCategoryCollection _categories;
		
		private ObservableInputGestureCollection _defaultGestures;
		
		/// <summary>
		/// Creates new instance of <see cref="InputBindingInfo"/>
		/// </summary>
		public InputBindingInfo() 
		{
			ActiveInputBindings = new InputBindingCollection();
			DefaultGestures = new ObservableInputGestureCollection();
			Categories = new InputBindingCategoryCollection();
			Groups = new BindingGroupCollection();
		}
		
		/// <summary>
		/// Gets or sets default gestures associated with bindings generated by 
		/// this <see cref="InputBindingInfo" />
		/// </summary>
		public ObservableInputGestureCollection DefaultGestures { 
			get {
				return _defaultGestures;
			}
			set {
				if(_defaultGestures != null) {
					_defaultGestures.CollectionChanged -= DefaultGestures_CollectionChanged;
				}
				
				if(value != null) {
					value.CollectionChanged += DefaultGestures_CollectionChanged;
				}
				
				var oldGestures = _defaultGestures;
				_defaultGestures = value;
				
				if(IsRegistered && (UserGestureManager.CurrentProfile == null || UserGestureManager.CurrentProfile[BindingInfoTemplate.CreateFromIBindingInfo(this)] == null)) {
					var description = new GesturesModificationDescription(
						BindingInfoTemplate.CreateFromIBindingInfo(this), 
						oldGestures != null ? oldGestures.InputGesturesCollection : new InputGestureCollection(),
						value != null ? value.InputGesturesCollection : new InputGestureCollection());
					
					SDCommandManager.InvokeGesturesChanged(this, new NotifyGesturesChangedEventArgs(description));
				}
			}
		}
		
		private void DefaultGestures_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) 
		{
			if(IsRegistered && (UserGestureManager.CurrentProfile == null || UserGestureManager.CurrentProfile[BindingInfoTemplate.CreateFromIBindingInfo(this)] == null)) {
				var newGestures = DefaultGestures.InputGesturesCollection;
				var oldGestures = new InputGestureCollection();
				oldGestures.AddRange(newGestures);
				
				if(e.Action == NotifyCollectionChangedAction.Add) {
					if(e.NewItems != null) {
						foreach(InputGesture ng in e.NewItems) { 
							oldGestures.Remove(ng); 
						}
					}
				} else if(e.Action == NotifyCollectionChangedAction.Remove) {
					if(e.OldItems != null) {
						foreach(InputGesture og in e.OldItems) { 
							oldGestures.Add(og); 
						}
					}
				}
				
				// When default gestures are active notify handlers about changes in gestures collection
				var description = new GesturesModificationDescription(BindingInfoTemplate.CreateFromIBindingInfo(this), oldGestures, newGestures);
				SDCommandManager.InvokeGesturesChanged(this, new NotifyGesturesChangedEventArgs(description));
			}
		}
		
		/// <summary>
		/// Gets <see cref="InputGestureCollection" /> used in generated input bindings
		/// </summary>
		public InputGestureCollection ActiveGestures { 
			get {
				if(UserGestureManager.CurrentProfile == null 
				   || UserGestureManager.CurrentProfile[BindingInfoTemplate.CreateFromIBindingInfo(this)] == null) {
					return DefaultGestures.InputGesturesCollection;
				} 
				
				return UserGestureManager.CurrentProfile[BindingInfoTemplate.CreateFromIBindingInfo(this)];
			}
		}
		
		/// <summary>
		/// Categories associated with <see cref="InputBindingInfo" />
		/// </summary>
		public InputBindingCategoryCollection Categories
		{
			get {
				return _categories;
			}
			set {
				if(value == null) {
					throw new ArgumentException("Categories collection can not be null");
				}
				
				var oldValue = _categories;
				_categories = value;
				
				SetCollectionChanged<InputBindingCategory>(oldValue, value, Categories_CollectionChanged);
			}
		}
		
		/// <summary>
		/// Gets <see cref="InputBindingCollection" /> generated by this <see cref="InputBindingInfo" />
		/// </summary>
		public InputBindingCollection ActiveInputBindings
		{
			get; private set;
		}
		
		/// <summary>
		/// Apply <see cref="ActiveInputBindings" /> to new <see cref="System.Windows.UIElement" /> collection
		/// </summary>
		/// <param name="newInstances">Collection of modified owner instances</param>
		protected override void PopulateOwnerInstancesWithBindings(ICollection<UIElement> newInstances)
		{
			// remove old bindings
			if(oldInstances != null) {
				foreach(var ownerInstance in oldInstances) {
					foreach(InputBinding binding in oldBindingCollection) {
						ownerInstance.InputBindings.Remove(binding);
					}
				}
			}
			
			oldInstances = new List<UIElement>();
	
			// appply new bindings
			if(newInstances != null) {
				foreach(var ownerInstance in newInstances) {
					ownerInstance.InputBindings.AddRange(ActiveInputBindings);
					oldInstances.Add(ownerInstance);
					
					// Sorting input bindings. This may be slow
					if(ownerInstance.InputBindings != null) {
						ownerInstance.InputBindings.SortByChords();
					}
				}
			}
		}
		
		/// <summary>
		/// Apply <see cref="ActiveInputBindings" /> to new <see cref="System.Type" /> collection
		/// </summary>
		/// <param name="newInstances">Collection of modified owner types</param>
		protected override void PopulateOwnerTypesWithBindings(ICollection<Type> newTypes)
		{
			// Remove old bindings
			if(oldTypes != null) {
				foreach(var ownerType in oldTypes) {
					foreach(InputBinding binding in oldBindingCollection) {
						SDCommandManager.RemoveClassInputBinding(ownerType, binding);
					}
				}
			}
			
			oldTypes = new List<Type>();
			
			// Apply new bindings
			if(newTypes != null) {
				foreach(var ownerType in newTypes) {
					foreach(InputBinding binding in ActiveInputBindings) {
						System.Windows.Input.CommandManager.RegisterClassInputBinding(ownerType, binding);
						oldTypes.Add(ownerType);
					}
					
					var fieldInfo = typeof(System.Windows.Input.CommandManager).GetField("_classInputBindings", BindingFlags.Static | BindingFlags.NonPublic);
					var fieldData = (HybridDictionary)fieldInfo.GetValue(null);
					var classInputBindings = (InputBindingCollection)fieldData[ownerType];
				
					// Sorting input bindings. This may be slow
					if(classInputBindings != null) {
						classInputBindings.SortByChords();
					}
				}
			}
		}
		
		private void Categories_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) 
		{
			if(e.NewItems != null) {
				foreach(InputBindingCategory addedCategory in e.NewItems) {
					if(!SDCommandManager.InputBindingCategories.Contains(addedCategory)) {
						throw new ArgumentException("InputBindingCategory is not registered in CommandManager");
					}
				}
			}
		}
		
		/// <summary>
		/// Re-generate <see cref="ActiveInputBindings" /> using <see cref="InputBindingInfo" /> data 
		/// </summary>
		protected override void GenerateBindings() 
		{			
			oldBindingCollection = ActiveInputBindings;
			
			ActiveInputBindings = new InputBindingCollection();
			foreach(InputGesture gesture in ActiveGestures) {
				var inputBinding = new InputBinding(RoutedCommand, gesture);
				ActiveInputBindings.Add(inputBinding);
			}
		}
	}
}
