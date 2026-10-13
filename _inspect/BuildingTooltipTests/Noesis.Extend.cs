using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using UnityEngine;

namespace Noesis;

internal class Extend
{
	public enum NativeTypeKind
	{
		Basic,
		Boxed,
		Component,
		Extended
	}

	public class NativeTypeInfo
	{
		public NativeTypeKind Kind { get; private set; }

		public Type Type { get; private set; }

		public NativeTypeInfo(NativeTypeKind kind, Type type)
		{
			Kind = kind;
			Type = type;
		}
	}

	public class NativeTypeComponentInfo : NativeTypeInfo
	{
		public Func<IntPtr, bool, BaseComponent> Creator { get; private set; }

		public NativeTypeComponentInfo(NativeTypeKind kind, Type type, Func<IntPtr, bool, BaseComponent> creator)
			: base(kind, type)
		{
			Creator = creator;
		}
	}

	public interface INativeTypeExtended
	{
	}

	public class NativeTypeEnumInfo : NativeTypeInfo, INativeTypeExtended
	{
		public NativeTypeEnumInfo(NativeTypeKind kind, Type type)
			: base(kind, type)
		{
		}
	}

	public class NativeTypeExtendedInfo : NativeTypeInfo, INativeTypeExtended
	{
		public Func<object> Creator { get; private set; }

		public NativeTypeExtendedInfo(NativeTypeKind kind, Type type, Func<object> creator)
			: base(kind, type)
		{
			Creator = creator;
		}
	}

	public class NativeTypePropsInfo : NativeTypeExtendedInfo
	{
		public List<PropertyAccessor> Properties { get; private set; }

		public NativeTypePropsInfo(NativeTypeKind kind, Type type, Func<object> creator)
			: base(kind, type, creator)
		{
			Properties = new List<PropertyAccessor>();
		}
	}

	public class NativeTypeIndexerInfo : NativeTypePropsInfo
	{
		public IndexerAccessor Indexer { get; private set; }

		public NativeTypeIndexerInfo(NativeTypeKind kind, Type type, Func<object> creator, IndexerAccessor indexer)
			: base(kind, type, creator)
		{
			Indexer = indexer;
		}
	}

	private struct ExtendTypeData
	{
		[MarshalAs(UnmanagedType.U8)]
		public long type;

		[MarshalAs(UnmanagedType.U8)]
		public long baseType;

		[MarshalAs(UnmanagedType.U8)]
		public long typeConverter;

		[MarshalAs(UnmanagedType.U8)]
		public long contentProperty;

		[MarshalAs(UnmanagedType.U4)]
		public int overrides;
	}

	[Flags]
	private enum ExtendTypeOverrides
	{
		None = 0,
		Object_ToString = 1,
		Object_Equals = 2,
		Visual_GetChildrenCount = 4,
		Visual_GetChild = 8,
		UIElement_OnRender = 0x10,
		FrameworkElement_ConnectEvent = 0x20,
		FrameworkElement_ConnectField = 0x40,
		FrameworkElement_Measure = 0x80,
		FrameworkElement_Arrange = 0x100,
		FrameworkElement_ApplyTemplate = 0x200,
		ItemsControl_GetContainer = 0x400,
		ItemsControl_IsContainer = 0x800,
		Adorner_GetTransform = 0x1000,
		Freezable_Clone = 0x2000,
		Animation_GetValueCore = 0x4000
	}

	private struct ExtendPropertyData
	{
		public long name;

		public long type;

		public long typeConverter;

		public int extendType;

		public int readOnly;
	}

	private delegate void Callback_FreeString(IntPtr strPtr);

	private delegate void Callback_RegisterType(string typeName);

	private delegate IntPtr Callback_ToString(IntPtr cPtr);

	private delegate bool Callback_Equals(IntPtr cPtr, IntPtr cPtrOtherType, IntPtr cPtrOther);

	private delegate int Callback_VisualChildrenCount(IntPtr cPtr, Visual.ChildrenCountBaseCallback callback);

	private delegate IntPtr Callback_VisualGetChild(IntPtr cPtr, int index, Visual.ChildrenCountBaseCallback countCallback, Visual.GetChildBaseCallback childCallback);

	private delegate void Callback_UIElementRender(IntPtr cPtr, IntPtr contextType, IntPtr context, UIElement.RenderBaseCallback callback);

	private delegate bool Callback_FrameworkElementConnectEvent(IntPtr cPtr, IntPtr cPtrSourceType, IntPtr cPtrSource, string eventName, string handlerName);

	private delegate void Callback_FrameworkElementConnectField(IntPtr cPtr, IntPtr cPtrSourceType, IntPtr cPtrSource, string fieldName);

	private delegate void Callback_FrameworkElementMeasure(IntPtr cPtr, ref Size availableSize, ref Size desiredSize, FrameworkElement.LayoutBaseCallback callback);

	private delegate void Callback_FrameworkElementArrange(IntPtr cPtr, ref Size finalSize, ref Size renderSize, FrameworkElement.LayoutBaseCallback callback);

	private delegate void Callback_FrameworkElementApplyTemplate(IntPtr cPtr);

	private delegate IntPtr Callback_ItemsControlGetContainer(IntPtr cPtr, ItemsControl.GetContainerForItemBaseCallback callback);

	private delegate bool Callback_ItemsControlIsContainer(IntPtr cPtr, IntPtr itemTypePtr, IntPtr itemPtr, ItemsControl.IsItemItsOwnContainerBaseCallback callback);

	private delegate void Callback_AdornerGetTransform(IntPtr cPtr, ref Matrix4 transform, ref Matrix4 desiredTransform);

	private delegate void Callback_FreezableClone(IntPtr cPtrType, IntPtr cPtrClone, IntPtr cPtrSource);

	private delegate IntPtr Callback_ShapeGetGeometry(IntPtr cPtr);

	private delegate bool Callback_CommandCanExecute(IntPtr cPtr, IntPtr paramType, IntPtr paramPtr);

	private delegate void Callback_CommandExecute(IntPtr cPtr, IntPtr paramType, IntPtr paramPtr);

	private delegate bool Callback_ConverterConvert(IntPtr cPtr, IntPtr valType, IntPtr valPtr, IntPtr targetTypePtr, IntPtr paramType, IntPtr paramPtr, out IntPtr result);

	private delegate bool Callback_ConverterConvertBack(IntPtr cPtr, IntPtr valType, IntPtr valPtr, IntPtr targetTypePtr, IntPtr paramType, IntPtr paramPtr, out IntPtr result);

	private delegate bool Callback_MultiConverterConvert(IntPtr cPtr, int numSources, IntPtr valTypes, IntPtr valPtrs, IntPtr targetTypePtr, IntPtr paramType, IntPtr paramPtr, out IntPtr result);

	private delegate bool Callback_MultiConverterConvertBack(IntPtr cPtr, int numSources, IntPtr valType, IntPtr valPtr, IntPtr targetTypePtrs, IntPtr paramType, IntPtr paramPtr, IntPtr results);

	private delegate uint Callback_ListCount(IntPtr cPtr);

	private delegate IntPtr Callback_ListGet(IntPtr cPtr, uint index);

	private delegate void Callback_ListSet(IntPtr cPtr, uint index, IntPtr itemType, IntPtr item);

	private delegate uint Callback_ListAdd(IntPtr cPtr, IntPtr itemType, IntPtr item);

	private delegate void Callback_ListInsert(IntPtr cPtr, uint index, IntPtr itemType, IntPtr item);

	private delegate int Callback_ListIndexOf(IntPtr cPtr, IntPtr itemType, IntPtr item);

	private delegate void Callback_ListRemoveAt(IntPtr cPtr, uint index);

	private delegate void Callback_ListClear(IntPtr cPtr);

	private delegate bool Callback_DictionaryFind(IntPtr cPtr, string key, ref IntPtr item);

	private delegate void Callback_DictionarySet(IntPtr cPtr, string key, IntPtr itemType, IntPtr item);

	private delegate void Callback_DictionaryAdd(IntPtr cPtr, string key, IntPtr itemType, IntPtr item);

	private delegate void Callback_DictionaryRemove(IntPtr cPtr, string key);

	private delegate bool Callback_ListIndexerTryGet(IntPtr cPtrType, IntPtr cPtr, uint index, ref IntPtr item);

	private delegate bool Callback_ListIndexerTrySet(IntPtr cPtrType, IntPtr cPtr, uint index, IntPtr itemType, IntPtr item);

	private delegate bool Callback_DictionaryIndexerTryGet(IntPtr cPtrType, IntPtr cPtr, string key, ref IntPtr item);

	private delegate bool Callback_DictionaryIndexerTrySet(IntPtr cPtrType, IntPtr cPtr, string key, IntPtr itemType, IntPtr item);

	private delegate IntPtr Callback_SelectTemplate(IntPtr cPtr, IntPtr itemType, IntPtr item, IntPtr containerType, IntPtr container);

	private delegate void Callback_StreamSetPosition(IntPtr cPtr, uint pos);

	private delegate uint Callback_StreamGetPosition(IntPtr cPtr);

	private delegate uint Callback_StreamGetLength(IntPtr cPtr);

	private delegate uint Callback_StreamRead(IntPtr cPtr, IntPtr buffer, uint bufferSize);

	private delegate void Callback_StreamClose(IntPtr cPtr);

	private delegate IntPtr Callback_ProviderLoadXaml(IntPtr cPtr, IntPtr filename);

	private delegate void Callback_ProviderTextureInfo(IntPtr cPtr, IntPtr filename, ref uint x, ref uint y, ref uint width, ref uint height, ref float dpiScale);

	private delegate IntPtr Callback_ProviderTextureLoad(IntPtr cPtr, IntPtr filename);

	private delegate IntPtr Callback_ProviderTextureOpen(IntPtr cPtr, IntPtr filename);

	private delegate IntPtr Callback_ProviderMatchFont(IntPtr cPtr, IntPtr baseUri, IntPtr familyName, ref int weight, ref int stretch, ref int style, ref uint index);

	private delegate bool Callback_ProviderFamilyExists(IntPtr cPtr, IntPtr baseUri, IntPtr familyName);

	private delegate void Callback_ProviderScanFolder(IntPtr cPtr, IntPtr folder);

	private delegate IntPtr Callback_ProviderOpenFont(IntPtr cPtr, IntPtr folder, IntPtr id);

	private delegate void Callback_ScrollInfoBringIntoView(IntPtr cPtr, int index);

	private delegate bool Callback_ScrollInfoGetCanHorizontalScroll(IntPtr cPtr);

	private delegate void Callback_ScrollInfoSetCanHorizontalScroll(IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] bool canScroll);

	private delegate bool Callback_ScrollInfoGetCanVerticalScroll(IntPtr cPtr);

	private delegate void Callback_ScrollInfoSetCanVerticalScroll(IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] bool canScroll);

	private delegate float Callback_ScrollInfoGetExtentWidth(IntPtr cPtr);

	private delegate float Callback_ScrollInfoGetExtentHeight(IntPtr cPtr);

	private delegate float Callback_ScrollInfoGetViewportWidth(IntPtr cPtr);

	private delegate float Callback_ScrollInfoGetViewportHeight(IntPtr cPtr);

	private delegate float Callback_ScrollInfoGetHorizontalOffset(IntPtr cPtr);

	private delegate float Callback_ScrollInfoGetVerticalOffset(IntPtr cPtr);

	private delegate IntPtr Callback_ScrollInfoGetScrollOwner(IntPtr cPtr);

	private delegate void Callback_ScrollInfoSetScrollOwner(IntPtr cPtr, IntPtr typeOwner, IntPtr cPtrOwner);

	private delegate void Callback_ScrollInfoLineLeft(IntPtr cPtr);

	private delegate void Callback_ScrollInfoLineRight(IntPtr cPtr);

	private delegate void Callback_ScrollInfoLineUp(IntPtr cPtr);

	private delegate void Callback_ScrollInfoLineDown(IntPtr cPtr);

	private delegate void Callback_ScrollInfoPageLeft(IntPtr cPtr);

	private delegate void Callback_ScrollInfoPageRight(IntPtr cPtr);

	private delegate void Callback_ScrollInfoPageUp(IntPtr cPtr);

	private delegate void Callback_ScrollInfoPageDown(IntPtr cPtr);

	private delegate void Callback_ScrollInfoMouseWheelLeft(IntPtr cPtr, float delta);

	private delegate void Callback_ScrollInfoMouseWheelRight(IntPtr cPtr, float delta);

	private delegate void Callback_ScrollInfoMouseWheelUp(IntPtr cPtr, float delta);

	private delegate void Callback_ScrollInfoMouseWheelDown(IntPtr cPtr, float delta);

	private delegate void Callback_ScrollInfoSetHorizontalOffset(IntPtr cPtr, float offset);

	private delegate void Callback_ScrollInfoSetVerticalOffset(IntPtr cPtr, float offset);

	private delegate void Callback_ScrollInfoMakeVisible(IntPtr cPtr, IntPtr visualType, IntPtr visualPtr, ref Rect rectangle, ref Rect result);

	private delegate IntPtr Callback_MarkupExtensionProvideValue(IntPtr cPtr, IntPtr provider);

	private delegate IntPtr Callback_AnimationTimelineGetType(IntPtr cPtr);

	private delegate IntPtr Callback_AnimationTimelineGetValue(IntPtr cPtr, IntPtr srcType, IntPtr srcPtr, IntPtr dstType, IntPtr dstPtr, IntPtr clockType, IntPtr clockPtr);

	private delegate bool Callback_BooleanAnimationGetValue(IntPtr cPtr, bool src, bool dst, IntPtr clockType, IntPtr clockPtr);

	private delegate short Callback_Int16AnimationGetValue(IntPtr cPtr, short src, short dst, IntPtr clockType, IntPtr clockPtr);

	private delegate int Callback_Int32AnimationGetValue(IntPtr cPtr, int src, int dst, IntPtr clockType, IntPtr clockPtr);

	private delegate long Callback_Int64AnimationGetValue(IntPtr cPtr, long src, long dst, IntPtr clockType, IntPtr clockPtr);

	private delegate float Callback_DoubleAnimationGetValue(IntPtr cPtr, float src, float dst, IntPtr clockType, IntPtr clockPtr);

	private delegate void Callback_ColorAnimationGetValue(IntPtr cPtr, ref Color src, ref Color dst, IntPtr clockType, IntPtr clockPtr, ref Color value);

	private delegate void Callback_PointAnimationGetValue(IntPtr cPtr, ref Point src, ref Point dst, IntPtr clockType, IntPtr clockPtr, ref Point value);

	private delegate void Callback_RectAnimationGetValue(IntPtr cPtr, ref Rect src, ref Rect dst, IntPtr clockType, IntPtr clockPtr, ref Rect value);

	private delegate void Callback_SizeAnimationGetValue(IntPtr cPtr, ref Size src, ref Size dst, IntPtr clockType, IntPtr clockPtr, ref Size value);

	private delegate void Callback_ThicknessAnimationGetValue(IntPtr cPtr, ref Thickness src, ref Thickness dst, IntPtr clockType, IntPtr clockPtr, ref Thickness value);

	private delegate IntPtr Callback_ObjectAnimationGetValue(IntPtr cPtr, IntPtr srcType, IntPtr srcPtr, IntPtr dstType, IntPtr dstPtr, IntPtr clockType, IntPtr clockPtr);

	private delegate IntPtr Callback_StringAnimationGetValue(IntPtr cPtr, IntPtr srcPtr, IntPtr dstPtr, IntPtr clockType, IntPtr clockPtr);

	private delegate void Callback_MatrixAnimationGetValue(IntPtr cPtr, ref Matrix src, ref Matrix dst, IntPtr clockType, IntPtr clockPtr, ref Matrix value);

	private enum NativePropertyType
	{
		Bool,
		Float,
		Double,
		Long,
		ULong,
		Int,
		UInt,
		Short,
		UShort,
		Color,
		Point,
		Rect,
		Int32Rect,
		Size,
		Thickness,
		CornerRadius,
		TimeSpan,
		Duration,
		KeyTime,
		NullableBool,
		NullableFloat,
		NullableDouble,
		NullableLong,
		NullableULong,
		NullableInt,
		NullableUInt,
		NullableShort,
		NullableUShort,
		NullableColor,
		NullablePoint,
		NullableRect,
		NullableInt32Rect,
		NullableSize,
		NullableThickness,
		NullableCornerRadius,
		NullableTimeSpan,
		NullableDuration,
		NullableKeyTime,
		Enum,
		String,
		Uri,
		Type,
		BaseComponent,
		Event
	}

	[return: MarshalAs(UnmanagedType.U1)]
	private delegate bool Callback_GetPropertyValue_Bool(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate float Callback_GetPropertyValue_Float(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate double Callback_GetPropertyValue_Double(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate long Callback_GetPropertyValue_Int64(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate ulong Callback_GetPropertyValue_UInt64(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate int Callback_GetPropertyValue_Int(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate uint Callback_GetPropertyValue_UInt(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate short Callback_GetPropertyValue_Short(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate ushort Callback_GetPropertyValue_UShort(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate IntPtr Callback_GetPropertyValue_String(IntPtr nativeType, int propertyIndex, IntPtr cPtr);

	private delegate IntPtr Callback_GetPropertyValue_Uri(IntPtr nativeType, int propertyIndex, IntPtr cPtr);

	private delegate void Callback_GetPropertyValue_Color(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Color value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate void Callback_GetPropertyValue_Point(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Point value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate void Callback_GetPropertyValue_Rect(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Rect value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate void Callback_GetPropertyValue_Int32Rect(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Int32Rect value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate void Callback_GetPropertyValue_Size(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Size value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate void Callback_GetPropertyValue_Thickness(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Thickness value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate void Callback_GetPropertyValue_CornerRadius(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref CornerRadius value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate void Callback_GetPropertyValue_TimeSpan(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref TimeSpanStruct value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate void Callback_GetPropertyValue_Duration(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Duration value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate void Callback_GetPropertyValue_KeyTime(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref KeyTime value, [MarshalAs(UnmanagedType.U1)] ref bool isNull);

	private delegate IntPtr Callback_GetPropertyValue_Type(IntPtr nativeType, int propertyIndex, IntPtr cPtr);

	private delegate IntPtr Callback_GetPropertyValue_BaseComponent(IntPtr nativeType, int propertyIndex, IntPtr cPtr);

	private delegate void Callback_SetPropertyValue_Bool(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] bool val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Float(IntPtr nativeType, int propertyIndex, IntPtr cPtr, float val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Double(IntPtr nativeType, int propertyIndex, IntPtr cPtr, double val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Int64(IntPtr nativeType, int propertyIndex, IntPtr cPtr, long val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_UInt64(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ulong val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Int(IntPtr nativeType, int propertyIndex, IntPtr cPtr, int val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_UInt(IntPtr nativeType, int propertyIndex, IntPtr cPtr, uint val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Short(IntPtr nativeType, int propertyIndex, IntPtr cPtr, short val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_UShort(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ushort val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_String(IntPtr nativeType, int propertyIndex, IntPtr cPtr, IntPtr val);

	private delegate void Callback_SetPropertyValue_Uri(IntPtr nativeType, int propertyIndex, IntPtr cPtr, IntPtr val);

	private delegate void Callback_SetPropertyValue_Color(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Color val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Point(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Point val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Rect(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Rect val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Int32Rect(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Int32Rect val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Size(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Size val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Thickness(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Thickness val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_CornerRadius(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref CornerRadius val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_TimeSpan(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref TimeSpanStruct val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Duration(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Duration val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_KeyTime(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref KeyTime val, [MarshalAs(UnmanagedType.U1)] bool isNull);

	private delegate void Callback_SetPropertyValue_Type(IntPtr nativeType, int propertyIndex, IntPtr cPtr, IntPtr val);

	private delegate void Callback_SetPropertyValue_BaseComponent(IntPtr nativeType, int propertyIndex, IntPtr cPtr, IntPtr valType, IntPtr val);

	private delegate void Callback_CreateInstance(IntPtr nativeType, IntPtr cPtr);

	private delegate void Callback_DeleteInstance(IntPtr cPtr);

	private delegate void Callback_GrabInstance(IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] bool grab);

	private class ExtendInfo
	{
		public object instance;

		public WeakReference weak;
	}

	private class ExtendNotifier
	{
		public IntPtr cPtr;

		~ExtendNotifier()
		{
			object obj = null;
			lock (_extends)
			{
				if (_extends.TryGetValue(cPtr.ToInt64(), out var value) && value.weak.IsAlive)
				{
					obj = value.weak.Target;
				}
			}
			if (obj != null)
			{
				_weakExtends.Remove(obj);
				_weakExtends.Add(obj, this);
				GC.ReRegisterForFinalize(this);
			}
			else
			{
				AddPendingRelease(cPtr);
			}
		}
	}

	private delegate IntPtr BoxDelegate(object val);

	private class Boxed<T>
	{
	}

	private delegate object UnboxDelegate(IntPtr cPtr);

	public class PropertyAccessor
	{
		private PropertyInfo _property;

		public PropertyInfo Property => _property;

		public virtual bool IsNullable => false;

		public PropertyAccessor(PropertyInfo p)
		{
			_property = p;
		}
	}

	public abstract class PropertyAccessorT<PropertyT> : PropertyAccessor
	{
		public PropertyAccessorT(PropertyInfo p)
			: base(p)
		{
		}

		public abstract PropertyT Get(object instance);

		public virtual void Set(object instance, PropertyT value)
		{
			throw new NotImplementedException();
		}
	}

	public class PropertyAccessorCastRW<PropertyT, SourceT> : PropertyAccessorT<PropertyT>
	{
		private PropertyAccessorT<SourceT> _prop;

		private Func<SourceT, PropertyT> _castTo;

		private Func<PropertyT, SourceT> _castFrom;

		public PropertyAccessorCastRW(PropertyInfo p, PropertyAccessorT<SourceT> prop, Func<SourceT, PropertyT> castTo, Func<PropertyT, SourceT> castFrom)
			: base(p)
		{
			_prop = prop;
			_castTo = castTo;
			_castFrom = castFrom;
		}

		public override PropertyT Get(object instance)
		{
			return _castTo(_prop.Get(instance));
		}

		public override void Set(object instance, PropertyT value)
		{
			_prop.Set(instance, _castFrom(value));
		}
	}

	public class PropertyAccessorCastRO<PropertyT, SourceT> : PropertyAccessorT<PropertyT>
	{
		private PropertyAccessorT<SourceT> _prop;

		private Func<SourceT, PropertyT> _castTo;

		public PropertyAccessorCastRO(PropertyInfo p, PropertyAccessorT<SourceT> prop, Func<SourceT, PropertyT> castTo)
			: base(p)
		{
			_prop = prop;
			_castTo = castTo;
		}

		public override PropertyT Get(object instance)
		{
			return _castTo(_prop.Get(instance));
		}
	}

	public class PropertyAccessorNullableCastRW<PropertyT, SourceT> : PropertyAccessorCastRW<PropertyT, SourceT>
	{
		public override bool IsNullable => true;

		public PropertyAccessorNullableCastRW(PropertyInfo p, PropertyAccessorT<SourceT> prop, Func<SourceT, PropertyT> castTo, Func<PropertyT, SourceT> castFrom)
			: base(p, prop, castTo, castFrom)
		{
		}
	}

	public class PropertyAccessorNullableCastRO<PropertyT, SourceT> : PropertyAccessorCastRO<PropertyT, SourceT>
	{
		public override bool IsNullable => true;

		public PropertyAccessorNullableCastRO(PropertyInfo p, PropertyAccessorT<SourceT> prop, Func<SourceT, PropertyT> castTo)
			: base(p, prop, castTo)
		{
		}
	}

	public class PropertyAccessorPropRW<PropertyT> : PropertyAccessorT<PropertyT>
	{
		private Func<object, PropertyT> _getter;

		private Action<object, PropertyT> _setter;

		public PropertyAccessorPropRW(PropertyInfo p)
			: base(p)
		{
			_getter = (object instance) => (PropertyT)p.GetValue(instance, null);
			_setter = delegate(object instance, PropertyT value)
			{
				p.SetValue(instance, value, null);
			};
		}

		public override PropertyT Get(object instance)
		{
			return _getter(instance);
		}

		public override void Set(object instance, PropertyT value)
		{
			_setter(instance, value);
		}
	}

	public class PropertyAccessorPropRO<PropertyT> : PropertyAccessorT<PropertyT>
	{
		private Func<object, PropertyT> _getter;

		public PropertyAccessorPropRO(PropertyInfo p)
			: base(p)
		{
			_getter = (object instance) => (PropertyT)p.GetValue(instance, null);
		}

		public override PropertyT Get(object instance)
		{
			return _getter(instance);
		}
	}

	public class PropertyAccessorNullablePropRW<PropertyT> : PropertyAccessorPropRW<PropertyT>
	{
		public override bool IsNullable => true;

		public PropertyAccessorNullablePropRW(PropertyInfo p)
			: base(p)
		{
		}
	}

	public class PropertyAccessorNullablePropRO<PropertyT> : PropertyAccessorPropRO<PropertyT>
	{
		public override bool IsNullable => true;

		public PropertyAccessorNullablePropRO(PropertyInfo p)
			: base(p)
		{
		}
	}

	public class PropertyAccessorRW<PropertyT> : PropertyAccessorT<PropertyT>
	{
		private Func<object, PropertyT> _getter;

		private Action<object, PropertyT> _setter;

		public PropertyAccessorRW(PropertyInfo p)
			: base(p)
		{
			_getter = CreateGetter<PropertyT>(p.DeclaringType, p.GetGetMethod());
			_setter = CreateSetter<PropertyT>(p.DeclaringType, p.GetSetMethod());
		}

		public override PropertyT Get(object instance)
		{
			return _getter(instance);
		}

		public override void Set(object instance, PropertyT value)
		{
			_setter(instance, value);
		}
	}

	public class PropertyAccessorRO<PropertyT> : PropertyAccessorT<PropertyT>
	{
		private Func<object, PropertyT> _getter;

		public PropertyAccessorRO(PropertyInfo p)
			: base(p)
		{
			_getter = CreateGetter<PropertyT>(p.DeclaringType, p.GetGetMethod());
		}

		public override PropertyT Get(object instance)
		{
			return _getter(instance);
		}
	}

	public class PropertyAccessorNullableRW<PropertyT> : PropertyAccessorRW<PropertyT>
	{
		public override bool IsNullable => true;

		public PropertyAccessorNullableRW(PropertyInfo p)
			: base(p)
		{
		}
	}

	public class PropertyAccessorNullableRO<PropertyT> : PropertyAccessorRO<PropertyT>
	{
		public override bool IsNullable => true;

		public PropertyAccessorNullableRO(PropertyInfo p)
			: base(p)
		{
		}
	}

	public abstract class IndexerAccessor
	{
	}

	public abstract class IndexerAccessorT<IndexT> : IndexerAccessor
	{
		public abstract object Get(object instance, IndexT index);

		public virtual void Set(object instance, IndexT index, object value)
		{
			throw new NotImplementedException();
		}
	}

	public abstract class IndexerAccessorPropT<IndexT> : IndexerAccessorT<IndexT>
	{
		private object[] _index = new object[1];

		protected object[] Index(IndexT index)
		{
			_index[0] = index;
			return _index;
		}
	}

	public class IndexerAccessorPropRW<IndexT> : IndexerAccessorPropT<IndexT>
	{
		private Func<object, IndexT, object> _getter;

		private Action<object, IndexT, object> _setter;

		public IndexerAccessorPropRW(PropertyInfo p)
		{
			IndexerAccessorPropRW<IndexT> indexerAccessorPropRW = this;
			_getter = (object instance, IndexT index) => p.GetValue(instance, indexerAccessorPropRW.Index(index));
			_setter = delegate(object instance, IndexT index, object value)
			{
				p.SetValue(instance, value, indexerAccessorPropRW.Index(index));
			};
		}

		public override object Get(object instance, IndexT index)
		{
			return _getter(instance, index);
		}

		public override void Set(object instance, IndexT index, object value)
		{
			_setter(instance, index, value);
		}
	}

	public class IndexerAccessorPropRO<IndexT> : IndexerAccessorPropT<IndexT>
	{
		private Func<object, IndexT, object> _getter;

		public IndexerAccessorPropRO(PropertyInfo p)
		{
			IndexerAccessorPropRO<IndexT> indexerAccessorPropRO = this;
			_getter = (object instance, IndexT index) => p.GetValue(instance, indexerAccessorPropRO.Index(index));
		}

		public override object Get(object instance, IndexT index)
		{
			return _getter(instance, index);
		}
	}

	public class IndexerAccessorRW<IndexT> : IndexerAccessorT<IndexT>
	{
		private Func<object, IndexT, object> _getter;

		private Action<object, IndexT, object> _setter;

		public IndexerAccessorRW(PropertyInfo p)
		{
			_getter = CreateIndexerGetter<IndexT>(p.DeclaringType, p.GetGetMethod());
			_setter = CreateIndexerSetter<IndexT>(p.DeclaringType, p.GetSetMethod());
		}

		public override object Get(object instance, IndexT index)
		{
			return _getter(instance, index);
		}

		public override void Set(object instance, IndexT index, object value)
		{
			_setter(instance, index, value);
		}
	}

	public class IndexerAccessorRO<IndexT> : IndexerAccessorT<IndexT>
	{
		private Func<object, IndexT, object> _getter;

		public IndexerAccessorRO(PropertyInfo p)
		{
			_getter = CreateIndexerGetter<IndexT>(p.DeclaringType, p.GetGetMethod());
		}

		public override object Get(object instance, IndexT index)
		{
			return _getter(instance, index);
		}
	}

	private delegate ExtendPropertyData AddPropertyDelegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo);

	private const int TypesCapacity = 650;

	private static Dictionary<long, NativeTypeInfo> _nativeTypes = new Dictionary<long, NativeTypeInfo>(650);

	private static Dictionary<Type, IntPtr> _managedTypes = new Dictionary<Type, IntPtr>(650);

	[ThreadStatic]
	private static object[] _objArgs1 = null;

	[ThreadStatic]
	private static Type[] _typeArgs0 = null;

	[ThreadStatic]
	private static Type[] _typeArgs1 = null;

	[ThreadStatic]
	private static Type[] _typeArgs2 = null;

	[ThreadStatic]
	private static Type[] _typeArgs3 = null;

	private static readonly Assembly BclAssembly = typeof(object).Assembly;

	private static HashSet<Type> _constructedTypes = new HashSet<Type>();

	private static Callback_FreeString _freeString = FreeString;

	[ThreadStatic]
	private static byte[] _byteBuffer = null;

	private static Callback_RegisterType _registerType = RegisterType;

	private static List<Assembly> _assemblies = null;

	private static Callback_ToString _toString = ToStringEx;

	private static Callback_Equals _equals = EqualsEx;

	private static Callback_VisualChildrenCount _visualChildrenCount = VisualChildrenCount;

	private static Callback_VisualGetChild _visualGetChild = VisualGetChild;

	private static Callback_UIElementRender _uiElementRender = UIElementRender;

	private static Callback_FrameworkElementConnectEvent _frameworkElementConnectEvent = FrameworkElementConnectEvent;

	private static Callback_FrameworkElementConnectField _frameworkElementConnectField = FrameworkElementConnectField;

	private static Callback_FrameworkElementMeasure _frameworkElementMeasure = FrameworkElementMeasure;

	private static Callback_FrameworkElementArrange _frameworkElementArrange = FrameworkElementArrange;

	private static Callback_FrameworkElementApplyTemplate _frameworkElementApplyTemplate = FrameworkElementApplyTemplate;

	private static Callback_ItemsControlGetContainer _itemsControlGetContainer = ItemsControlGetContainer;

	private static Callback_ItemsControlIsContainer _itemsControlIsContainer = ItemsControlIsContainer;

	private static Callback_AdornerGetTransform _adornerGetTransform = AdornerGetTransform;

	private static Callback_FreezableClone _freezableClone = FreezableClone;

	private static Callback_ShapeGetGeometry _shapeGetGeometry = ShapeGetGeometry;

	private static Callback_CommandCanExecute _commandCanExecute = CommandCanExecute;

	private static Callback_CommandExecute _commandExecute = CommandExecute;

	private static Callback_ConverterConvert _converterConvert = ConverterConvert;

	private static Callback_ConverterConvertBack _converterConvertBack = ConverterConvertBack;

	private static Callback_MultiConverterConvert _multiConverterConvert = MultiConverterConvert;

	private static Callback_MultiConverterConvertBack _multiConverterConvertBack = MultiConverterConvertBack;

	private static Callback_ListCount _listCount = ListCount;

	private static Callback_ListGet _listGet = ListGet;

	private static Callback_ListSet _listSet = ListSet;

	private static Callback_ListAdd _listAdd = ListAdd;

	private static Callback_ListInsert _listInsert = ListInsert;

	private static Callback_ListIndexOf _listIndexOf = ListIndexOf;

	private static Callback_ListRemoveAt _listRemoveAt = ListRemoveAt;

	private static Callback_ListClear _listClear = ListClear;

	private static Callback_DictionaryFind _dictionaryFind = DictionaryFind;

	private static Callback_DictionarySet _dictionarySet = DictionarySet;

	private static Callback_DictionaryAdd _dictionaryAdd = DictionaryAdd;

	private static Callback_DictionaryRemove _dictionaryRemove = DictionaryRemove;

	private static Callback_ListIndexerTryGet _listIndexerTryGet = ListIndexerTryGet;

	private static Callback_ListIndexerTrySet _listIndexerTrySet = ListIndexerTrySet;

	private static Callback_DictionaryIndexerTryGet _dictionaryIndexerTryGet = DictionaryIndexerTryGet;

	private static Callback_DictionaryIndexerTrySet _dictionaryIndexerTrySet = DictionaryIndexerTrySet;

	private static Callback_SelectTemplate _selectTemplate = SelectTemplate;

	private static Callback_StreamSetPosition _streamSetPosition = StreamSetPosition;

	private static Callback_StreamGetPosition _streamGetPosition = StreamGetPosition;

	private static Callback_StreamGetLength _streamGetLength = StreamGetLength;

	private static Callback_StreamRead _streamRead = StreamRead;

	private static Callback_StreamClose _streamClose = StreamClose;

	private static Callback_ProviderLoadXaml _providerLoadXaml = ProviderLoadXaml;

	private static Callback_ProviderTextureInfo _providerTextureInfo = ProviderTextureInfo;

	private static Callback_ProviderTextureLoad _providerTextureLoad = ProviderTextureLoad;

	private static Callback_ProviderTextureOpen _providerTextureOpen = ProviderTextureOpen;

	private static Callback_ProviderMatchFont _providerMatchFont = ProviderMatchFont;

	private static Callback_ProviderFamilyExists _providerFamilyExists = ProviderFamilyExists;

	private static Callback_ProviderScanFolder _providerScanFolder = ProviderScanFolder;

	private static Callback_ProviderOpenFont _providerOpenFont = ProviderOpenFont;

	private static Callback_ScrollInfoBringIntoView _scrollInfoBringIntoView = ScrollInfoBringIntoView;

	private static Callback_ScrollInfoGetCanHorizontalScroll _scrollInfoGetCanHorizontalScroll = ScrollInfoGetCanHorizontalScroll;

	private static Callback_ScrollInfoSetCanHorizontalScroll _scrollInfoSetCanHorizontalScroll = ScrollInfoSetCanHorizontalScroll;

	private static Callback_ScrollInfoGetCanVerticalScroll _scrollInfoGetCanVerticalScroll = ScrollInfoGetCanVerticalScroll;

	private static Callback_ScrollInfoSetCanVerticalScroll _scrollInfoSetCanVerticalScroll = ScrollInfoSetCanVerticalScroll;

	private static Callback_ScrollInfoGetExtentWidth _scrollInfoGetExtentWidth = ScrollInfoGetExtentWidth;

	private static Callback_ScrollInfoGetExtentHeight _scrollInfoGetExtentHeight = ScrollInfoGetExtentHeight;

	private static Callback_ScrollInfoGetViewportWidth _scrollInfoGetViewportWidth = ScrollInfoGetViewportWidth;

	private static Callback_ScrollInfoGetViewportHeight _scrollInfoGetViewportHeight = ScrollInfoGetViewportHeight;

	private static Callback_ScrollInfoGetHorizontalOffset _scrollInfoGetHorizontalOffset = ScrollInfoGetHorizontalOffset;

	private static Callback_ScrollInfoGetVerticalOffset _scrollInfoGetVerticalOffset = ScrollInfoGetVerticalOffset;

	private static Callback_ScrollInfoGetScrollOwner _scrollInfoGetScrollOwner = ScrollInfoGetScrollOwner;

	private static Callback_ScrollInfoSetScrollOwner _scrollInfoSetScrollOwner = ScrollInfoSetScrollOwner;

	private static Callback_ScrollInfoLineLeft _scrollInfoLineLeft = ScrollInfoLineLeft;

	private static Callback_ScrollInfoLineRight _scrollInfoLineRight = ScrollInfoLineRight;

	private static Callback_ScrollInfoLineUp _scrollInfoLineUp = ScrollInfoLineUp;

	private static Callback_ScrollInfoLineDown _scrollInfoLineDown = ScrollInfoLineDown;

	private static Callback_ScrollInfoPageLeft _scrollInfoPageLeft = ScrollInfoPageLeft;

	private static Callback_ScrollInfoPageRight _scrollInfoPageRight = ScrollInfoPageRight;

	private static Callback_ScrollInfoPageUp _scrollInfoPageUp = ScrollInfoPageUp;

	private static Callback_ScrollInfoPageDown _scrollInfoPageDown = ScrollInfoPageDown;

	private static Callback_ScrollInfoMouseWheelLeft _scrollInfoMouseWheelLeft = ScrollInfoMouseWheelLeft;

	private static Callback_ScrollInfoMouseWheelRight _scrollInfoMouseWheelRight = ScrollInfoMouseWheelRight;

	private static Callback_ScrollInfoMouseWheelUp _scrollInfoMouseWheelUp = ScrollInfoMouseWheelUp;

	private static Callback_ScrollInfoMouseWheelDown _scrollInfoMouseWheelDown = ScrollInfoMouseWheelDown;

	private static Callback_ScrollInfoSetHorizontalOffset _scrollInfoSetHorizontalOffset = ScrollInfoSetHorizontalOffset;

	private static Callback_ScrollInfoSetVerticalOffset _scrollInfoSetVerticalOffset = ScrollInfoSetVerticalOffset;

	private static Callback_ScrollInfoMakeVisible _scrollInfoMakeVisible = ScrollInfoMakeVisible;

	private static Callback_MarkupExtensionProvideValue _markupExtensionProvideValue = MarkupExtensionProvideValue;

	private static Callback_AnimationTimelineGetType _animationTimelineGetType = AnimationTimelineGetType;

	private static Callback_AnimationTimelineGetValue _animationTimelineGetValue = AnimationTimelineGetValue;

	private static Callback_BooleanAnimationGetValue _booleanAnimationGetValue = BooleanAnimationGetValue;

	private static Callback_Int16AnimationGetValue _int16AnimationGetValue = Int16AnimationGetValue;

	private static Callback_Int32AnimationGetValue _int32AnimationGetValue = Int32AnimationGetValue;

	private static Callback_Int64AnimationGetValue _int64AnimationGetValue = Int64AnimationGetValue;

	private static Callback_DoubleAnimationGetValue _doubleAnimationGetValue = DoubleAnimationGetValue;

	private static Callback_ColorAnimationGetValue _colorAnimationGetValue = ColorAnimationGetValue;

	private static Callback_PointAnimationGetValue _pointAnimationGetValue = PointAnimationGetValue;

	private static Callback_RectAnimationGetValue _rectAnimationGetValue = RectAnimationGetValue;

	private static Callback_SizeAnimationGetValue _sizeAnimationGetValue = SizeAnimationGetValue;

	private static Callback_ThicknessAnimationGetValue _thicknessAnimationGetValue = ThicknessAnimationGetValue;

	private static Callback_ObjectAnimationGetValue _objectAnimationGetValue = ObjectAnimationGetValue;

	private static Callback_StringAnimationGetValue _stringAnimationGetValue = StringAnimationGetValue;

	private static Callback_MatrixAnimationGetValue _matrixAnimationGetValue = MatrixAnimationGetValue;

	private static Callback_GetPropertyValue_Bool _getPropertyValue_Bool = GetPropertyValue_Bool;

	private static Callback_GetPropertyValue_Float _getPropertyValue_Float = GetPropertyValue_Float;

	private static Callback_GetPropertyValue_Double _getPropertyValue_Double = GetPropertyValue_Double;

	private static Callback_GetPropertyValue_Int64 _getPropertyValue_Int64 = GetPropertyValue_Int64;

	private static Callback_GetPropertyValue_UInt64 _getPropertyValue_UInt64 = GetPropertyValue_UInt64;

	private static Callback_GetPropertyValue_Int _getPropertyValue_Int = GetPropertyValue_Int;

	private static Callback_GetPropertyValue_UInt _getPropertyValue_UInt = GetPropertyValue_UInt;

	private static Callback_GetPropertyValue_Short _getPropertyValue_Short = GetPropertyValue_Short;

	private static Callback_GetPropertyValue_UShort _getPropertyValue_UShort = GetPropertyValue_UShort;

	private static Callback_GetPropertyValue_String _getPropertyValue_String = GetPropertyValue_String;

	private static Callback_GetPropertyValue_Uri _getPropertyValue_Uri = GetPropertyValue_Uri;

	private static Callback_GetPropertyValue_Color _getPropertyValue_Color = GetPropertyValue_Color;

	private static Callback_GetPropertyValue_Point _getPropertyValue_Point = GetPropertyValue_Point;

	private static Callback_GetPropertyValue_Rect _getPropertyValue_Rect = GetPropertyValue_Rect;

	private static Callback_GetPropertyValue_Int32Rect _getPropertyValue_Int32Rect = GetPropertyValue_Int32Rect;

	private static Callback_GetPropertyValue_Size _getPropertyValue_Size = GetPropertyValue_Size;

	private static Callback_GetPropertyValue_Thickness _getPropertyValue_Thickness = GetPropertyValue_Thickness;

	private static Callback_GetPropertyValue_CornerRadius _getPropertyValue_CornerRadius = GetPropertyValue_CornerRadius;

	private static Callback_GetPropertyValue_TimeSpan _getPropertyValue_TimeSpan = GetPropertyValue_TimeSpan;

	private static Callback_GetPropertyValue_Duration _getPropertyValue_Duration = GetPropertyValue_Duration;

	private static Callback_GetPropertyValue_KeyTime _getPropertyValue_KeyTime = GetPropertyValue_KeyTime;

	private static Callback_GetPropertyValue_Type _getPropertyValue_Type = GetPropertyValue_Type;

	private static Callback_GetPropertyValue_BaseComponent _getPropertyValue_BaseComponent = GetPropertyValue_BaseComponent;

	private static Callback_SetPropertyValue_Bool _setPropertyValue_Bool = SetPropertyValue_Bool;

	private static Callback_SetPropertyValue_Float _setPropertyValue_Float = SetPropertyValue_Float;

	private static Callback_SetPropertyValue_Double _setPropertyValue_Double = SetPropertyValue_Double;

	private static Callback_SetPropertyValue_Int64 _setPropertyValue_Int64 = SetPropertyValue_Int64;

	private static Callback_SetPropertyValue_UInt64 _setPropertyValue_UInt64 = SetPropertyValue_UInt64;

	private static Callback_SetPropertyValue_Int _setPropertyValue_Int = SetPropertyValue_Int;

	private static Callback_SetPropertyValue_UInt _setPropertyValue_UInt = SetPropertyValue_UInt;

	private static Callback_SetPropertyValue_Short _setPropertyValue_Short = SetPropertyValue_Short;

	private static Callback_SetPropertyValue_UShort _setPropertyValue_UShort = SetPropertyValue_UShort;

	private static Callback_SetPropertyValue_String _setPropertyValue_String = SetPropertyValue_String;

	private static Callback_SetPropertyValue_Uri _setPropertyValue_Uri = SetPropertyValue_Uri;

	private static Callback_SetPropertyValue_Color _setPropertyValue_Color = SetPropertyValue_Color;

	private static Callback_SetPropertyValue_Point _setPropertyValue_Point = SetPropertyValue_Point;

	private static Callback_SetPropertyValue_Rect _setPropertyValue_Rect = SetPropertyValue_Rect;

	private static Callback_SetPropertyValue_Int32Rect _setPropertyValue_Int32Rect = SetPropertyValue_Int32Rect;

	private static Callback_SetPropertyValue_Size _setPropertyValue_Size = SetPropertyValue_Size;

	private static Callback_SetPropertyValue_Thickness _setPropertyValue_Thickness = SetPropertyValue_Thickness;

	private static Callback_SetPropertyValue_CornerRadius _setPropertyValue_CornerRadius = SetPropertyValue_CornerRadius;

	private static Callback_SetPropertyValue_TimeSpan _setPropertyValue_TimeSpan = SetPropertyValue_TimeSpan;

	private static Callback_SetPropertyValue_Duration _setPropertyValue_Duration = SetPropertyValue_Duration;

	private static Callback_SetPropertyValue_KeyTime _setPropertyValue_KeyTime = SetPropertyValue_KeyTime;

	private static Callback_SetPropertyValue_Type _setPropertyValue_Type = SetPropertyValue_Type;

	private static Callback_SetPropertyValue_BaseComponent _setPropertyValue_BaseComponent = SetPropertyValue_BaseComponent;

	[ThreadStatic]
	private static IntPtr _cPtr = IntPtr.Zero;

	[ThreadStatic]
	private static Type _extendType = null;

	private static Callback_CreateInstance _createInstance = CreateInstance;

	private static Callback_DeleteInstance _deleteInstance = DeleteInstance;

	private static Callback_GrabInstance _grabInstance = GrabInstance;

	private static Dictionary<long, ExtendInfo> _extends = new Dictionary<long, ExtendInfo>();

	private static ConditionalWeakTable<object, ExtendNotifier> _weakExtends = new ConditionalWeakTable<object, ExtendNotifier>();

	private static Dictionary<long, WeakReference> _proxies = new Dictionary<long, WeakReference>();

	private static List<IntPtr> _pendingRelease = new List<IntPtr>(256);

	private static Dictionary<Type, BoxDelegate> _boxFunctions = CreateBoxFunctions();

	private static Dictionary<Type, UnboxDelegate> _unboxFunctions = CreateUnboxFunctions();

	private static MethodInfo CreateGetterHelperMethod = typeof(Extend).GetMethod("CreateGetterHelper", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic);

	private static MethodInfo CreateSetterHelperMethod = typeof(Extend).GetMethod("CreateSetterHelper", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic);

	private static MethodInfo CreateIndexerGetterHelperMethod = typeof(Extend).GetMethod("CreateIndexerGetterHelper", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic);

	private static MethodInfo CreateIndexerSetterHelperMethod = typeof(Extend).GetMethod("CreateIndexerSetterHelper", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic);

	private static Dictionary<Type, AddPropertyDelegate> _addPropertyFunctions = AddPropertyFunctions();

	public static bool Initialized { get; internal set; }

	public static void Init()
	{
		Noesis_EnableExtend(enable: true);
		Initialized = true;
		RegisterNativeTypes();
	}

	public static void Shutdown()
	{
		if (!Initialized)
		{
			return;
		}
		try
		{
			Initialized = false;
			EventHandlerStore.Clear();
			GUI.SetApplicationResources(null);
			List<KeyValuePair<long, WeakReference>> list = new List<KeyValuePair<long, WeakReference>>();
			list.AddRange(_proxies);
			foreach (KeyValuePair<long, ExtendInfo> extend in _extends)
			{
				list.Add(new KeyValuePair<long, WeakReference>(extend.Key, extend.Value.weak));
				extend.Value.instance = null;
			}
			GC.Collect();
			GC.WaitForPendingFinalizers();
			foreach (KeyValuePair<long, WeakReference> item in list)
			{
				object target = item.Value.Target;
				if (target != null)
				{
					BaseComponent.ForceRelease(target, new IntPtr(item.Key));
				}
			}
			ReleasePending();
		}
		catch (Exception)
		{
		}
		Noesis_ClearExtendTypes();
		Noesis_EnableExtend(enable: false);
		ClearTables();
	}

	public static void RegisterCallbacks()
	{
		Noesis_RegisterReflectionCallbacks(_freeString, _registerType, _toString, _equals, _visualChildrenCount, _visualGetChild, _uiElementRender, _frameworkElementConnectEvent, _frameworkElementConnectField, _frameworkElementMeasure, _frameworkElementArrange, _frameworkElementApplyTemplate, _itemsControlGetContainer, _itemsControlIsContainer, _adornerGetTransform, _freezableClone, _shapeGetGeometry, _commandCanExecute, _commandExecute, _converterConvert, _converterConvertBack, _multiConverterConvert, _multiConverterConvertBack, _listCount, _listGet, _listSet, _listAdd, _listInsert, _listIndexOf, _listRemoveAt, _listClear, _dictionaryFind, _dictionarySet, _dictionaryAdd, _dictionaryRemove, _listIndexerTryGet, _listIndexerTrySet, _dictionaryIndexerTryGet, _dictionaryIndexerTrySet, _selectTemplate, _streamSetPosition, _streamGetPosition, _streamGetLength, _streamRead, _streamClose, _providerLoadXaml, _providerTextureInfo, _providerTextureLoad, _providerTextureOpen, _providerMatchFont, _providerFamilyExists, _providerScanFolder, _providerOpenFont, _scrollInfoBringIntoView, _scrollInfoGetCanHorizontalScroll, _scrollInfoSetCanHorizontalScroll, _scrollInfoGetCanVerticalScroll, _scrollInfoSetCanVerticalScroll, _scrollInfoGetExtentWidth, _scrollInfoGetExtentHeight, _scrollInfoGetViewportWidth, _scrollInfoGetViewportHeight, _scrollInfoGetHorizontalOffset, _scrollInfoGetVerticalOffset, _scrollInfoGetScrollOwner, _scrollInfoSetScrollOwner, _scrollInfoLineLeft, _scrollInfoLineRight, _scrollInfoLineUp, _scrollInfoLineDown, _scrollInfoPageLeft, _scrollInfoPageRight, _scrollInfoPageUp, _scrollInfoPageDown, _scrollInfoMouseWheelLeft, _scrollInfoMouseWheelRight, _scrollInfoMouseWheelUp, _scrollInfoMouseWheelDown, _scrollInfoSetHorizontalOffset, _scrollInfoSetVerticalOffset, _scrollInfoMakeVisible, _markupExtensionProvideValue, _animationTimelineGetType, _animationTimelineGetValue, _booleanAnimationGetValue, _int16AnimationGetValue, _int32AnimationGetValue, _int64AnimationGetValue, _doubleAnimationGetValue, _colorAnimationGetValue, _pointAnimationGetValue, _rectAnimationGetValue, _sizeAnimationGetValue, _thicknessAnimationGetValue, _objectAnimationGetValue, _stringAnimationGetValue, _matrixAnimationGetValue, _getPropertyValue_Bool, _getPropertyValue_Float, _getPropertyValue_Double, _getPropertyValue_Int64, _getPropertyValue_UInt64, _getPropertyValue_Int, _getPropertyValue_UInt, _getPropertyValue_Short, _getPropertyValue_UShort, _getPropertyValue_String, _getPropertyValue_Uri, _getPropertyValue_Color, _getPropertyValue_Point, _getPropertyValue_Rect, _getPropertyValue_Int32Rect, _getPropertyValue_Size, _getPropertyValue_Thickness, _getPropertyValue_CornerRadius, _getPropertyValue_TimeSpan, _getPropertyValue_Duration, _getPropertyValue_KeyTime, _getPropertyValue_Type, _getPropertyValue_BaseComponent, _setPropertyValue_Bool, _setPropertyValue_Float, _setPropertyValue_Double, _setPropertyValue_Int64, _setPropertyValue_UInt64, _setPropertyValue_Int, _setPropertyValue_UInt, _setPropertyValue_Short, _setPropertyValue_UShort, _setPropertyValue_String, _setPropertyValue_Uri, _setPropertyValue_Color, _setPropertyValue_Point, _setPropertyValue_Rect, _setPropertyValue_Int32Rect, _setPropertyValue_Size, _setPropertyValue_Thickness, _setPropertyValue_CornerRadius, _setPropertyValue_TimeSpan, _setPropertyValue_Duration, _setPropertyValue_KeyTime, _setPropertyValue_Type, _setPropertyValue_BaseComponent, _createInstance, _deleteInstance, _grabInstance);
	}

	public static void UnregisterCallbacks()
	{
		Noesis_RegisterReflectionCallbacks(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
	}

	private static void ClearTables()
	{
		_nativeTypes.Clear();
		_managedTypes.Clear();
		_extends.Clear();
		_proxies.Clear();
		_pendingRelease.Clear();
		PropertyMetadata.ClearCallbacks();
	}

	private static void AddNativeType(IntPtr nativeType, NativeTypeInfo info)
	{
		if (_nativeTypes.TryGetValue(nativeType.ToInt64(), out var value))
		{
			Log.Error($"Native type already registered for type {value.Type.FullName}, registering {info.Type.FullName}");
		}
		_nativeTypes[nativeType.ToInt64()] = info;
		if (_managedTypes.TryGetValue(info.Type, out var _))
		{
			Log.Error($"Native type already registered for type {info.Type.FullName}");
		}
		_managedTypes[info.Type] = nativeType;
	}

	public static IntPtr TryGetNativeType(Type type)
	{
		lock (_managedTypes)
		{
			if (_managedTypes.TryGetValue(type, out var value))
			{
				return value;
			}
		}
		return IntPtr.Zero;
	}

	public static IntPtr GetNativeType(Type type)
	{
		if (!_managedTypes.TryGetValue(type, out var value))
		{
			throw new InvalidOperationException("Native type is not registered for '" + type.Name + "'");
		}
		return value;
	}

	public static NativeTypeInfo GetNativeTypeInfo(IntPtr nativeType)
	{
		if (!_nativeTypes.TryGetValue(nativeType.ToInt64(), out var value))
		{
			IntPtr baseType = BaseComponent.GetBaseType(nativeType);
			if (baseType != IntPtr.Zero)
			{
				return GetNativeTypeInfo(baseType);
			}
			string text = StringFromNativeUtf8(Noesis_GetTypeName(nativeType));
			throw new InvalidOperationException("Native type '" + text + "' is not registered");
		}
		return value;
	}

	internal static object GetProxy(IntPtr nativeType, IntPtr cPtr, bool ownMemory)
	{
		if (cPtr != IntPtr.Zero)
		{
			NativeTypeInfo nativeTypeInfo = GetNativeTypeInfo(nativeType);
			return nativeTypeInfo.Kind switch
			{
				NativeTypeKind.Boxed => Unbox(cPtr, ownMemory, nativeTypeInfo), 
				NativeTypeKind.Component => GetProxyInstance(cPtr, ownMemory, nativeTypeInfo), 
				NativeTypeKind.Extended => GetExtendInstance(cPtr, ownMemory), 
				_ => throw new InvalidOperationException($"Can't get a proxy for basic type {nativeTypeInfo.Type.Name}"), 
			};
		}
		return null;
	}

	public static object GetProxy(IntPtr cPtr, bool ownMemory)
	{
		if (cPtr != IntPtr.Zero)
		{
			NativeTypeInfo nativeTypeInfo = GetNativeTypeInfo(BaseComponent.GetDynamicType(cPtr));
			return nativeTypeInfo.Kind switch
			{
				NativeTypeKind.Boxed => Unbox(cPtr, ownMemory, nativeTypeInfo), 
				NativeTypeKind.Component => GetProxyInstance(cPtr, ownMemory, nativeTypeInfo), 
				NativeTypeKind.Extended => GetExtendInstance(cPtr, ownMemory), 
				_ => throw new InvalidOperationException($"Can't get a proxy for basic type {nativeTypeInfo.Type.Name}"), 
			};
		}
		return null;
	}

	public static object Initialize(object instance)
	{
		DependencyObject dependencyObject = instance as DependencyObject;
		if (dependencyObject != null && !(dependencyObject is FrameworkElement))
		{
			dependencyObject.InitObject();
		}
		return instance;
	}

	public static void RegisterNativeTypes()
	{
		IntPtr[] array = new IntPtr[650];
		for (int i = 0; i < 650; i++)
		{
			array[i] = IntPtr.Zero;
		}
		int num = Noesis_GetNativeTypes(array, 650);
		int num2 = 0;
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Type)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(string)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(bool)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(float)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(double)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(int)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(uint)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(short)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ushort)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(long)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ulong)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Color)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Point)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Rect)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Int32Rect)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Size)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Thickness)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(CornerRadius)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(GridLength)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Duration)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(KeyTime)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(TimeSpan)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(VirtualizationCacheLength)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Matrix)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Matrix3D)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Matrix4)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(bool?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(float?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(double?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(int?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(uint?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(short?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ushort?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(long?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ulong?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Color?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Point?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Rect?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Int32Rect?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Size?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Thickness?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(CornerRadius?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Duration?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(KeyTime?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(TimeSpan?)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(AlignmentX)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(AlignmentY)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(AutoToolTipPlacement)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(BindingMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(BitmapScalingMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(BrushMappingMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(CharacterCasing)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ClickMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ColorInterpolationMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Dock)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ExpandDirection)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(FillRule)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(FlowDirection)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(FontStretch)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(FontStyle)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(FontWeight)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(GeometryCombineMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(GradientSpreadMethod)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(HorizontalAlignment)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(MouseAction)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Key)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ModifierKeys)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(KeyboardNavigationMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(LineStackingStrategy)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ListSortDirection)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(MenuItemRole)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Orientation)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(OverflowMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(PenLineCap)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(PenLineJoin)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(PlacementMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(PopupAnimation)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(RelativeSourceMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(SelectionMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ScrollBarVisibility)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Stretch)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(StretchDirection)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(TextDecorations)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(TextAlignment)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(TextTrimming)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(TextWrapping)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(TickBarPlacement)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(TickPlacement)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(TileMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(VerticalAlignment)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Visibility)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ClockState)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(EasingMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(SlipBehavior)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(FillBehavior)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(GridViewColumnHeaderRole)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(HandoffBehavior)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(PanningMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(UpdateSourceTrigger)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(ScrollUnit)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(VirtualizationMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(VirtualizationCacheLengthUnit)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(PPAAMode)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Type>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<string>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<bool>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<float>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<double>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<int>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<uint>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<short>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ushort>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<long>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ulong>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Color>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Point>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Rect>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Int32Rect>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Size>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Thickness>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<CornerRadius>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<GridLength>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Duration>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<KeyTime>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<TimeSpan>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<VirtualizationCacheLength>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Boxed<Matrix>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Boxed<Matrix3D>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Basic, typeof(Boxed<Matrix4>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Enum>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<AlignmentX>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<AlignmentY>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<AutoToolTipPlacement>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<BindingMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<BitmapScalingMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<BrushMappingMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<CharacterCasing>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ClickMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ColorInterpolationMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Dock>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ExpandDirection>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<FillRule>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<FlowDirection>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<FontStretch>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<FontStyle>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<FontWeight>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<GeometryCombineMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<GradientSpreadMethod>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<HorizontalAlignment>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<MouseAction>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Key>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ModifierKeys>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<KeyboardNavigationMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<LineStackingStrategy>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ListSortDirection>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<MenuItemRole>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Orientation>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<OverflowMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<PenLineCap>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<PenLineJoin>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<PlacementMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<PopupAnimation>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<RelativeSourceMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<SelectionMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ScrollBarVisibility>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Stretch>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<StretchDirection>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<TextDecorations>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<TextAlignment>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<TextTrimming>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<TextWrapping>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<TickBarPlacement>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<TickPlacement>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<TileMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<VerticalAlignment>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<Visibility>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ClockState>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<EasingMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<SlipBehavior>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<FillBehavior>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<GridViewColumnHeaderRole>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<HandoffBehavior>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<PanningMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<UpdateSourceTrigger>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<ScrollUnit>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<VirtualizationMode>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<VirtualizationCacheLengthUnit>)));
		AddNativeType(array[num2++], new NativeTypeInfo(NativeTypeKind.Boxed, typeof(Boxed<PPAAMode>)));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BaseComponent), BaseComponent.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DispatcherObject), DispatcherObject.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DependencyObject), DependencyObject.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DependencyProperty), DependencyProperty.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Freezable), Freezable.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PropertyMetadata), PropertyMetadata.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Expression), Expression.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(View), View.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Renderer), Renderer.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Adorner), Adorner.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(AdornerLayer), AdornerLayer.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(AdornerDecorator), AdornerDecorator.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Animatable), Animatable.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ArcSegment), ArcSegment.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BindingBase), null));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BindingExpressionBase), BindingExpressionBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BindingExpression), BindingExpression.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Bold), Bold.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ButtonBase), ButtonBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DefinitionBase), DefinitionBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MenuBase), MenuBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SetterBase), SetterBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SetterBaseCollection), SetterBaseCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TextBoxBase), TextBoxBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TriggerBase), TriggerBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BezierSegment), BezierSegment.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Binding), Binding.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BindingCollection), BindingCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BitmapImage), BitmapImage.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BitmapSource), BitmapSource.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Border), Border.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Brush), Brush.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BrushShader), null));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BulletDecorator), BulletDecorator.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Button), Button.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Canvas), Canvas.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CheckBox), CheckBox.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BaseUICollection), BaseUICollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CollectionView), CollectionView.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CollectionViewSource), CollectionViewSource.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ColumnDefinition), ColumnDefinition.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ColumnDefinitionCollection), ColumnDefinitionCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CombinedGeometry), CombinedGeometry.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ComboBox), ComboBox.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ComboBoxItem), ComboBoxItem.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CommandBinding), CommandBinding.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CommandBindingCollection), CommandBindingCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CompositeTransform), CompositeTransform.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CompositeTransform3D), CompositeTransform3D.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Condition), Condition.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ConditionCollection), ConditionCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ContentControl), ContentControl.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ContentPresenter), ContentPresenter.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ContextMenu), ContextMenu.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ContextMenuService), null));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Control), Control.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ControlTemplate), ControlTemplate.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CroppedBitmap), CroppedBitmap.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Cursor), Cursor.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DashStyle), DashStyle.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DataTemplate), DataTemplate.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DataTemplateSelector), DataTemplateSelector.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DataTrigger), DataTrigger.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(HierarchicalDataTemplate), HierarchicalDataTemplate.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Decorator), Decorator.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DockPanel), DockPanel.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DrawingContext), DrawingContext.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Effect), Effect.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BlurEffect), BlurEffect.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DropShadowEffect), DropShadowEffect.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ShaderEffect), null));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Ellipse), Ellipse.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EllipseGeometry), EllipseGeometry.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EventTrigger), EventTrigger.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Expander), Expander.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(FontFamily), FontFamily.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(FormattedText), FormattedText.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(FrameworkElement), FrameworkElement.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(FrameworkTemplate), FrameworkTemplate.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(FrameworkPropertyMetadata), FrameworkPropertyMetadata.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BaseFreezableCollection), BaseFreezableCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Geometry), Geometry.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GeometryCollection), GeometryCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GeometryGroup), GeometryGroup.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GradientBrush), GradientBrush.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GradientStop), GradientStop.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GradientStopCollection), GradientStopCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Grid), Grid.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GridSplitter), GridSplitter.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GroupBox), GroupBox.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(HeaderedContentControl), HeaderedContentControl.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(HeaderedItemsControl), HeaderedItemsControl.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Hyperlink), Hyperlink.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Image), Image.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ImageBrush), ImageBrush.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ImageSource), ImageSource.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Inline), Inline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(InlineCollection), InlineCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(InlineUIContainer), InlineUIContainer.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(InputBinding), InputBinding.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(InputBindingCollection), InputBindingCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(InputGesture), InputGesture.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(InputGestureCollection), InputGestureCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Italic), Italic.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ItemCollection), ItemCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ItemContainerGenerator), ItemContainerGenerator.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ItemsControl), ItemsControl.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ItemsPanelTemplate), ItemsPanelTemplate.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ItemsPresenter), ItemsPresenter.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(KeyBinding), KeyBinding.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Keyboard), Keyboard.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(KeyboardNavigation), KeyboardNavigation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(KeyGesture), KeyGesture.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Label), Label.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Line), Line.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearGradientBrush), LinearGradientBrush.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LineBreak), LineBreak.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LineGeometry), LineGeometry.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LineSegment), LineSegment.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ListBox), ListBox.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ListBoxItem), ListBoxItem.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MarkupExtension), null));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MatrixTransform), MatrixTransform.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MatrixTransform3D), MatrixTransform3D.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Menu), Menu.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MenuItem), MenuItem.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MouseBinding), MouseBinding.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MouseGesture), MouseGesture.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MultiBinding), MultiBinding.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MultiBindingExpression), MultiBindingExpression.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MultiDataTrigger), MultiDataTrigger.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MultiTrigger), MultiTrigger.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(NameScope), NameScope.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Page), Page.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Panel), Panel.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PasswordBox), PasswordBox.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Path), Path.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PathFigure), PathFigure.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PathFigureCollection), PathFigureCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PathGeometry), PathGeometry.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PathSegment), PathSegment.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PathSegmentCollection), PathSegmentCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Pen), Pen.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PointCollection), PointCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PolyBezierSegment), PolyBezierSegment.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PolyLineSegment), PolyLineSegment.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PolyQuadraticBezierSegment), PolyQuadraticBezierSegment.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Popup), Popup.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ProgressBar), ProgressBar.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PropertyPath), PropertyPath.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(QuadraticBezierSegment), QuadraticBezierSegment.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RadialGradientBrush), RadialGradientBrush.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RadioButton), RadioButton.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RangeBase), RangeBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Rectangle), Rectangle.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RectangleGeometry), RectangleGeometry.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RelativeSource), RelativeSource.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RenderOptions), null));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RepeatButton), RepeatButton.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ResourceDictionary), ResourceDictionary.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ResourceDictionaryCollection), ResourceDictionaryCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RotateTransform), RotateTransform.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RoutedCommand), RoutedCommand.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RoutedEvent), RoutedEvent.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RoutedUICommand), RoutedUICommand.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RowDefinition), RowDefinition.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RowDefinitionCollection), RowDefinitionCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Run), Run.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ScaleTransform), ScaleTransform.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ScrollBar), ScrollBar.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ScrollViewer), ScrollViewer.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ScrollContentPresenter), ScrollContentPresenter.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Selector), Selector.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Separator), Separator.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Setter), Setter.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Shape), FrameworkElement.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SkewTransform), SkewTransform.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Slider), Slider.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SolidColorBrush), SolidColorBrush.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Span), Span.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(StackPanel), StackPanel.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(StatusBar), StatusBar.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(StatusBarItem), StatusBarItem.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(StreamGeometry), StreamGeometry.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Style), Style.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TabControl), TabControl.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TabItem), TabItem.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TabPanel), TabPanel.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TemplateBindingExpression), TemplateBindingExpression.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TemplateBindingExtension), TemplateBindingExtension.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TextBlock), TextBlock.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TextBox), TextBox.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TextElement), TextElement.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TextureSource), TextureSource.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DynamicTextureSource), DynamicTextureSource.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Thumb), Thumb.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TickBar), TickBar.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TileBrush), TileBrush.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ToggleButton), ToggleButton.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ToolBar), ToolBar.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ToolBarOverflowPanel), ToolBarOverflowPanel.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ToolBarPanel), ToolBarPanel.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ToolBarTray), ToolBarTray.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ToolTip), ToolTip.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ToolTipService), null));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Track), Track.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TransformGroup), TransformGroup.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TranslateTransform), TranslateTransform.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Transform), Transform.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Transform3D), Transform3D.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TransformCollection), TransformCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TreeView), TreeView.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TreeViewItem), TreeViewItem.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TriggerAction), TriggerAction.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TriggerActionCollection), TriggerActionCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Trigger), Trigger.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TriggerCollection), TriggerCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(UIElement), UIElement.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(UIElementCollection), UIElementCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(UIPropertyMetadata), UIPropertyMetadata.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Underline), Underline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(UniformGrid), UniformGrid.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(UserControl), UserControl.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Viewbox), Viewbox.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VirtualizingPanel), VirtualizingPanel.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VirtualizingStackPanel), VirtualizingStackPanel.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Visual), Visual.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VisualBrush), VisualBrush.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VisualCollection), VisualCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(WrapPanel), WrapPanel.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Clock), Clock.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ClockGroup), ClockGroup.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(AnimationClock), AnimationClock.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Timeline), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TimelineCollection), TimelineCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(TimelineGroup), TimelineGroup.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ParallelTimeline), ParallelTimeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(AnimationTimeline), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Storyboard), Storyboard.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BooleanAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int16AnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int32AnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int64AnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DoubleAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ColorAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PointAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RectAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SizeAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ThicknessAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ObjectAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(StringAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MatrixAnimationBase), Timeline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int16Animation), Int16Animation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int32Animation), Int32Animation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int64Animation), Int64Animation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DoubleAnimation), DoubleAnimation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ColorAnimation), ColorAnimation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PointAnimation), PointAnimation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RectAnimation), RectAnimation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SizeAnimation), SizeAnimation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ThicknessAnimation), ThicknessAnimation.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BooleanAnimationUsingKeyFrames), BooleanAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int16AnimationUsingKeyFrames), Int16AnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int32AnimationUsingKeyFrames), Int32AnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int64AnimationUsingKeyFrames), Int64AnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DoubleAnimationUsingKeyFrames), DoubleAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ColorAnimationUsingKeyFrames), ColorAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PointAnimationUsingKeyFrames), PointAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RectAnimationUsingKeyFrames), RectAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SizeAnimationUsingKeyFrames), SizeAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ThicknessAnimationUsingKeyFrames), ThicknessAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ObjectAnimationUsingKeyFrames), ObjectAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(StringAnimationUsingKeyFrames), StringAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MatrixAnimationUsingKeyFrames), MatrixAnimationUsingKeyFrames.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BooleanKeyFrameCollection), BooleanKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int16KeyFrameCollection), Int16KeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int32KeyFrameCollection), Int32KeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int64KeyFrameCollection), Int64KeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DoubleKeyFrameCollection), DoubleKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ColorKeyFrameCollection), ColorKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PointKeyFrameCollection), PointKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RectKeyFrameCollection), RectKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SizeKeyFrameCollection), SizeKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ThicknessKeyFrameCollection), ThicknessKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ObjectKeyFrameCollection), ObjectKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(StringKeyFrameCollection), StringKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MatrixKeyFrameCollection), MatrixKeyFrameCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BooleanKeyFrame), BooleanKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int16KeyFrame), Int16KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int32KeyFrame), Int32KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Int64KeyFrame), Int64KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DoubleKeyFrame), DoubleKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ColorKeyFrame), ColorKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PointKeyFrame), PointKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RectKeyFrame), RectKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SizeKeyFrame), SizeKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ThicknessKeyFrame), ThicknessKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(StringKeyFrame), StringKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(MatrixKeyFrame), MatrixKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteBooleanKeyFrame), DiscreteBooleanKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteInt16KeyFrame), DiscreteInt16KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteInt32KeyFrame), DiscreteInt32KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteInt64KeyFrame), DiscreteInt64KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteDoubleKeyFrame), DiscreteDoubleKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteColorKeyFrame), DiscreteColorKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscretePointKeyFrame), DiscretePointKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteRectKeyFrame), DiscreteRectKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteSizeKeyFrame), DiscreteSizeKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteThicknessKeyFrame), DiscreteThicknessKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteObjectKeyFrame), DiscreteObjectKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteStringKeyFrame), DiscreteStringKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(DiscreteMatrixKeyFrame), DiscreteMatrixKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearInt16KeyFrame), LinearInt16KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearInt32KeyFrame), LinearInt32KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearInt64KeyFrame), LinearInt64KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearDoubleKeyFrame), LinearDoubleKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearColorKeyFrame), LinearColorKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearPointKeyFrame), LinearPointKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearRectKeyFrame), LinearRectKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearSizeKeyFrame), LinearSizeKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(LinearThicknessKeyFrame), LinearThicknessKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SplineInt16KeyFrame), SplineInt16KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SplineInt32KeyFrame), SplineInt32KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SplineInt64KeyFrame), SplineInt64KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SplineDoubleKeyFrame), SplineDoubleKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SplineColorKeyFrame), SplineColorKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SplinePointKeyFrame), SplinePointKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SplineRectKeyFrame), SplineRectKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SplineSizeKeyFrame), SplineSizeKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SplineThicknessKeyFrame), SplineThicknessKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(KeySpline), KeySpline.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingInt16KeyFrame), EasingInt16KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingInt32KeyFrame), EasingInt32KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingInt64KeyFrame), EasingInt64KeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingDoubleKeyFrame), EasingDoubleKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingColorKeyFrame), EasingColorKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingPointKeyFrame), EasingPointKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingRectKeyFrame), EasingRectKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingSizeKeyFrame), EasingSizeKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingThicknessKeyFrame), EasingThicknessKeyFrame.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(EasingFunctionBase), EasingFunctionBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BackEase), BackEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BounceEase), BounceEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CircleEase), CircleEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(CubicEase), CubicEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ElasticEase), ElasticEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ExponentialEase), ExponentialEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PowerEase), PowerEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(QuadraticEase), QuadraticEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(QuarticEase), QuarticEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(QuinticEase), QuinticEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(SineEase), SineEase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ControllableStoryboardAction), ControllableStoryboardAction.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(BeginStoryboard), BeginStoryboard.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(PauseStoryboard), PauseStoryboard.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ResumeStoryboard), ResumeStoryboard.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(StopStoryboard), StopStoryboard.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VisualStateManager), VisualStateManager.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VisualStateGroup), VisualStateGroup.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VisualStateGroupCollection), VisualStateGroupCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VisualTransition), VisualTransition.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VisualTransitionCollection), VisualTransitionCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VisualState), VisualState.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(VisualStateCollection), VisualStateCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ViewBase), ViewBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GridView), GridView.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GridViewColumn), GridViewColumn.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GridViewColumnCollection), GridViewColumnCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GridViewColumnHeader), GridViewColumnHeader.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GridViewRowPresenterBase), GridViewRowPresenterBase.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GridViewRowPresenter), GridViewRowPresenter.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(GridViewHeaderRowPresenter), GridViewHeaderRowPresenter.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ListView), ListView.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(ListViewItem), ListViewItem.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RiveInput), RiveInput.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RiveInputCollection), RiveInputCollection.CreateProxy));
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RiveControl), RiveControl.CreateProxy));
		_managedTypes[typeof(NativeRenderDevice)] = array[num2];
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RenderDevice), NativeRenderDevice.CreateProxy));
		_managedTypes[typeof(NativeRenderTarget)] = array[num2];
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(RenderTarget), NativeRenderTarget.CreateProxy));
		_managedTypes[typeof(NativeTexture)] = array[num2];
		AddNativeType(array[num2++], new NativeTypeComponentInfo(NativeTypeKind.Component, typeof(Texture), NativeTexture.CreateProxy));
		_managedTypes[typeof(object)] = BaseComponent.GetStaticType();
		if (num2 != num)
		{
			throw new InvalidOperationException("Invalid registered native types count");
		}
	}

	public static void UnregisterNativeTypes()
	{
		Shutdown();
		if (_assemblies != null)
		{
			AppDomain.CurrentDomain.AssemblyLoad -= AddLoadedAssembly;
			_assemblies = null;
		}
		Init();
	}

	public static IntPtr RegisterNativeType(Type type)
	{
		return RegisterNativeType(type, registerDP: true);
	}

	public static IntPtr RegisterNativeType(Type type, bool registerDP)
	{
		IntPtr value = IntPtr.Zero;
		if (type == null)
		{
			return IntPtr.Zero;
		}
		if (_objArgs1 == null)
		{
			_objArgs1 = new object[1];
		}
		if (_typeArgs0 == null)
		{
			_typeArgs0 = new Type[0];
		}
		if (_typeArgs1 == null)
		{
			_typeArgs1 = new Type[1];
		}
		if (_typeArgs2 == null)
		{
			_typeArgs2 = new Type[2];
		}
		if (_typeArgs3 == null)
		{
			_typeArgs3 = new Type[3];
		}
		PropertyInfo[] publicProperties;
		NativeTypeInfo info;
		lock (_managedTypes)
		{
			if (_managedTypes.TryGetValue(type, out value))
			{
				return value;
			}
			if (type.GetTypeInfo().IsInterface)
			{
				value = BaseComponent.GetStaticType();
				_managedTypes[type] = value;
				return value;
			}
			if (type.GetTypeInfo().IsEnum)
			{
				int numEnums;
				IntPtr intPtr = CreateNativeEnumsData(type, out numEnums);
				try
				{
					value = Noesis_RegisterEnumType(TypeFullName(type), numEnums, intPtr);
				}
				finally
				{
					Marshal.FreeHGlobal(intPtr);
				}
				AddNativeType(value, new NativeTypeEnumInfo(NativeTypeKind.Basic, type));
				return value;
			}
			PropertyInfo propertyInfo = null;
			IndexerAccessor indexer = null;
			if (typeof(UnityEngine.Texture).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = TextureSource.Extend(TypeFullName(type));
			}
			else if (typeof(Sprite).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = CroppedBitmap.Extend(TypeFullName(type));
			}
			else if (typeof(IScrollInfo).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()) && typeof(VirtualizingPanel).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = ExtendVirtualScrollInfo.Extend(TypeFullName(type));
			}
			else if (typeof(IScrollInfo).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()) && typeof(Panel).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = ExtendScrollInfo.Extend(TypeFullName(type));
			}
			else if (type.GetTypeInfo().IsSubclassOf(typeof(BaseComponent)))
			{
				MethodInfo methodInfo = FindExtendMethod(type);
				if (!(methodInfo != null))
				{
					throw new InvalidOperationException("Can't find Extend method in any base class for " + type.FullName);
				}
				_objArgs1[0] = TypeFullName(type);
				value = (IntPtr)methodInfo.Invoke(null, _objArgs1);
			}
			else if (typeof(Stream).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = ExtendStream.Extend(TypeFullName(type));
			}
			else if (typeof(ICommand).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = ExtendCommand.Extend(TypeFullName(type));
			}
			else if (typeof(IValueConverter).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = ExtendConverter.Extend(TypeFullName(type));
			}
			else if (typeof(IMultiValueConverter).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = ExtendMultiConverter.Extend(TypeFullName(type));
			}
			else if (typeof(IList).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = ((!typeof(INotifyCollectionChanged).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo())) ? ExtendList.Extend(TypeFullName(type)) : ExtendObservableList.Extend(TypeFullName(type)));
			}
			else if (typeof(IDictionary).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				value = ExtendDictionary.Extend(TypeFullName(type));
			}
			else if ((propertyInfo = FindListIndexer(type)) != null)
			{
				indexer = CreateIndexerAccessor<int>(propertyInfo);
				value = ExtendListIndexer.Extend(TypeFullName(type));
			}
			else if ((propertyInfo = FindDictIndexer(type)) != null)
			{
				indexer = CreateIndexerAccessor<string>(propertyInfo);
				value = ExtendDictionaryIndexer.Extend(TypeFullName(type));
			}
			else
			{
				value = BaseComponent.Extend(TypeFullName(type));
			}
			publicProperties = GetPublicProperties(type);
			info = CreateNativeTypeInfo(type, indexer, publicProperties);
			AddNativeType(value, info);
		}
		ExtendTypeData typeData = CreateNativeTypeData(type, value);
		int numProps;
		IntPtr intPtr2 = CreateNativePropsData(type, publicProperties, info, out numProps);
		try
		{
			Noesis_FillExtendType(ref typeData, numProps, intPtr2);
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr2);
		}
		if (registerDP)
		{
			RegisterDependencyProperties(type);
		}
		return value;
	}

	private static string TypeFullName(Type type)
	{
		return type.FullName.Replace("Culture=,", "Culture=neutral,");
	}

	private static PropertyInfo[] GetPublicProperties(Type type)
	{
		return type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
	}

	private static EventInfo[] GetPublicEvents(Type type)
	{
		return type.GetEvents(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
	}

	private static PropertyInfo FindListIndexer(Type type)
	{
		_typeArgs1[0] = typeof(int);
		return type.GetProperty("Item", BindingFlags.Instance | BindingFlags.Public, null, null, _typeArgs1, null);
	}

	private static PropertyInfo FindDictIndexer(Type type)
	{
		_typeArgs1[0] = typeof(string);
		return type.GetProperty("Item", BindingFlags.Instance | BindingFlags.Public, null, null, _typeArgs1, null);
	}

	private static Func<object> TypeCreator(Type type)
	{
		if (!type.GetTypeInfo().IsValueType && Platform.ID != PlatformID.iPhone)
		{
			ConstructorInfo constructor = type.GetTypeInfo().GetConstructor(Type.EmptyTypes);
			if (constructor != null && !type.GetTypeInfo().IsAbstract)
			{
				return System.Linq.Expressions.Expression.Lambda<Func<object>>(System.Linq.Expressions.Expression.New(constructor), Array.Empty<ParameterExpression>()).Compile();
			}
			return null;
		}
		return () => Activator.CreateInstance(type);
	}

	private static NativeTypeInfo CreateNativeTypeInfo(Type type, IndexerAccessor indexer, PropertyInfo[] props)
	{
		if (indexer != null)
		{
			return new NativeTypeIndexerInfo(NativeTypeKind.Extended, type, TypeCreator(type), indexer);
		}
		if (props.Length != 0)
		{
			return new NativeTypePropsInfo(NativeTypeKind.Extended, type, TypeCreator(type));
		}
		return new NativeTypeExtendedInfo(NativeTypeKind.Extended, type, TypeCreator(type));
	}

	private static bool IsOverride(MethodInfo m)
	{
		if (m != null)
		{
			return m.GetBaseDefinition().DeclaringType != m.DeclaringType;
		}
		return false;
	}

	public static MethodInfo FindMethod(Type type, string name, Type[] types)
	{
		return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, types, null);
	}

	public static PropertyInfo FindProperty(Type type, string name)
	{
		return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}

	private static ExtendTypeData CreateNativeTypeData(Type type, IntPtr nativeType)
	{
		ExtendTypeData result = new ExtendTypeData
		{
			type = nativeType.ToInt64()
		};
		if (typeof(UnityEngine.Texture).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			result.baseType = TryGetNativeType(typeof(TextureSource)).ToInt64();
		}
		else if (typeof(Sprite).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			result.baseType = TryGetNativeType(typeof(CroppedBitmap)).ToInt64();
		}
		else
		{
			result.baseType = EnsureNativeType(type.GetTypeInfo().BaseType).ToInt64();
		}
		TypeConverterAttribute customAttribute = type.GetTypeInfo().GetCustomAttribute<TypeConverterAttribute>();
		if (customAttribute != null)
		{
			result.typeConverter = Marshal.StringToHGlobalAnsi(customAttribute.ConverterTypeName).ToInt64();
		}
		ContentPropertyAttribute customAttribute2 = type.GetTypeInfo().GetCustomAttribute<ContentPropertyAttribute>();
		if (customAttribute2 != null)
		{
			result.contentProperty = Marshal.StringToHGlobalAnsi(customAttribute2.Name).ToInt64();
		}
		ExtendTypeOverrides extendTypeOverrides = ExtendTypeOverrides.None;
		if (IsOverride(FindMethod(type, "ToString", _typeArgs0)))
		{
			extendTypeOverrides |= ExtendTypeOverrides.Object_ToString;
		}
		_typeArgs1[0] = typeof(object);
		if (IsOverride(FindMethod(type, "Equals", _typeArgs1)))
		{
			extendTypeOverrides |= ExtendTypeOverrides.Object_Equals;
		}
		if (typeof(Visual).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			if (IsOverride(FindProperty(type, "VisualChildrenCount")?.GetMethod))
			{
				extendTypeOverrides |= ExtendTypeOverrides.Visual_GetChildrenCount;
			}
			_typeArgs1[0] = typeof(int);
			if (IsOverride(FindMethod(type, "GetVisualChild", _typeArgs1)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.Visual_GetChild;
			}
		}
		if (typeof(UIElement).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			_typeArgs1[0] = typeof(DrawingContext);
			if (IsOverride(FindMethod(type, "OnRender", _typeArgs1)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.UIElement_OnRender;
			}
		}
		if (typeof(FrameworkElement).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			_typeArgs3[0] = typeof(object);
			_typeArgs3[1] = typeof(string);
			_typeArgs3[2] = typeof(string);
			if (IsOverride(FindMethod(type, "ConnectEvent", _typeArgs3)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.FrameworkElement_ConnectEvent;
			}
			_typeArgs2[0] = typeof(object);
			_typeArgs2[1] = typeof(string);
			if (IsOverride(FindMethod(type, "ConnectField", _typeArgs2)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.FrameworkElement_ConnectField;
			}
			_typeArgs1[0] = typeof(Size);
			if (IsOverride(FindMethod(type, "MeasureOverride", _typeArgs1)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.FrameworkElement_Measure;
			}
			if (IsOverride(FindMethod(type, "ArrangeOverride", _typeArgs1)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.FrameworkElement_Arrange;
			}
			if (IsOverride(FindMethod(type, "OnApplyTemplate", _typeArgs0)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.FrameworkElement_ApplyTemplate;
			}
		}
		if (typeof(ItemsControl).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			if (IsOverride(FindMethod(type, "GetContainerForItemOverride", _typeArgs0)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.ItemsControl_GetContainer;
			}
			_typeArgs1[0] = typeof(object);
			if (IsOverride(FindMethod(type, "IsItemItsOwnContainerOverride", _typeArgs1)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.ItemsControl_IsContainer;
			}
		}
		if (typeof(Adorner).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			_typeArgs1[0] = typeof(Matrix4);
			if (IsOverride(FindMethod(type, "GetDesiredTransform", _typeArgs1)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.Adorner_GetTransform;
			}
		}
		if (typeof(Freezable).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			_typeArgs1[0] = typeof(Freezable);
			if (IsOverride(FindMethod(type, "CloneCommonCore", _typeArgs1)))
			{
				extendTypeOverrides |= ExtendTypeOverrides.Freezable_Clone;
			}
		}
		if (typeof(AnimationTimeline).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			if (typeof(Int16Animation).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				_typeArgs3[0] = typeof(short);
				_typeArgs3[1] = typeof(short);
				_typeArgs3[2] = typeof(AnimationClock);
				MethodInfo methodInfo = FindMethod(type, "GetCurrentValueCore", _typeArgs3);
				if (IsOverride(methodInfo) && methodInfo.DeclaringType != typeof(Int16Animation))
				{
					extendTypeOverrides |= ExtendTypeOverrides.Animation_GetValueCore;
				}
			}
			if (typeof(Int32Animation).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				_typeArgs3[0] = typeof(int);
				_typeArgs3[1] = typeof(int);
				_typeArgs3[2] = typeof(AnimationClock);
				MethodInfo methodInfo2 = FindMethod(type, "GetCurrentValueCore", _typeArgs3);
				if (IsOverride(methodInfo2) && methodInfo2.DeclaringType != typeof(Int32Animation))
				{
					extendTypeOverrides |= ExtendTypeOverrides.Animation_GetValueCore;
				}
			}
			if (typeof(Int64Animation).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				_typeArgs3[0] = typeof(long);
				_typeArgs3[1] = typeof(long);
				_typeArgs3[2] = typeof(AnimationClock);
				MethodInfo methodInfo3 = FindMethod(type, "GetCurrentValueCore", _typeArgs3);
				if (IsOverride(methodInfo3) && methodInfo3.DeclaringType != typeof(Int64Animation))
				{
					extendTypeOverrides |= ExtendTypeOverrides.Animation_GetValueCore;
				}
			}
			if (typeof(DoubleAnimation).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				_typeArgs3[0] = typeof(float);
				_typeArgs3[1] = typeof(float);
				_typeArgs3[2] = typeof(AnimationClock);
				MethodInfo methodInfo4 = FindMethod(type, "GetCurrentValueCore", _typeArgs3);
				if (IsOverride(methodInfo4) && methodInfo4.DeclaringType != typeof(DoubleAnimation))
				{
					extendTypeOverrides |= ExtendTypeOverrides.Animation_GetValueCore;
				}
			}
			if (typeof(ColorAnimation).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				_typeArgs3[0] = typeof(Color);
				_typeArgs3[1] = typeof(Color);
				_typeArgs3[2] = typeof(AnimationClock);
				MethodInfo methodInfo5 = FindMethod(type, "GetCurrentValueCore", _typeArgs3);
				if (IsOverride(methodInfo5) && methodInfo5.DeclaringType != typeof(ColorAnimation))
				{
					extendTypeOverrides |= ExtendTypeOverrides.Animation_GetValueCore;
				}
			}
			if (typeof(PointAnimation).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				_typeArgs3[0] = typeof(Point);
				_typeArgs3[1] = typeof(Point);
				_typeArgs3[2] = typeof(AnimationClock);
				MethodInfo methodInfo6 = FindMethod(type, "GetCurrentValueCore", _typeArgs3);
				if (IsOverride(methodInfo6) && methodInfo6.DeclaringType != typeof(PointAnimation))
				{
					extendTypeOverrides |= ExtendTypeOverrides.Animation_GetValueCore;
				}
			}
			if (typeof(RectAnimation).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				_typeArgs3[0] = typeof(Rect);
				_typeArgs3[1] = typeof(Rect);
				_typeArgs3[2] = typeof(AnimationClock);
				MethodInfo methodInfo7 = FindMethod(type, "GetCurrentValueCore", _typeArgs3);
				if (IsOverride(methodInfo7) && methodInfo7.DeclaringType != typeof(RectAnimation))
				{
					extendTypeOverrides |= ExtendTypeOverrides.Animation_GetValueCore;
				}
			}
			if (typeof(SizeAnimation).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				_typeArgs3[0] = typeof(Size);
				_typeArgs3[1] = typeof(Size);
				_typeArgs3[2] = typeof(AnimationClock);
				MethodInfo methodInfo8 = FindMethod(type, "GetCurrentValueCore", _typeArgs3);
				if (IsOverride(methodInfo8) && methodInfo8.DeclaringType != typeof(SizeAnimation))
				{
					extendTypeOverrides |= ExtendTypeOverrides.Animation_GetValueCore;
				}
			}
			if (typeof(ThicknessAnimation).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
			{
				_typeArgs3[0] = typeof(Thickness);
				_typeArgs3[1] = typeof(Thickness);
				_typeArgs3[2] = typeof(AnimationClock);
				MethodInfo methodInfo9 = FindMethod(type, "GetCurrentValueCore", _typeArgs3);
				if (IsOverride(methodInfo9) && methodInfo9.DeclaringType != typeof(ThicknessAnimation))
				{
					extendTypeOverrides |= ExtendTypeOverrides.Animation_GetValueCore;
				}
			}
		}
		result.overrides = (int)extendTypeOverrides;
		return result;
	}

	private static bool IsIndexerProperty(PropertyInfo p)
	{
		return p.GetIndexParameters().Length != 0;
	}

	private static bool HasTypeConverter(PropertyInfo p)
	{
		return p.GetCustomAttribute<TypeConverterAttribute>() != null;
	}

	private static bool IsDependencyProperty(Type type, PropertyInfo prop)
	{
		string name = prop.Name + "Property";
		FieldInfo field = type.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
		if (field != null)
		{
			return field.FieldType.Equals(typeof(DependencyProperty));
		}
		return false;
	}

	private static IntPtr CreateNativePropsData(Type type, PropertyInfo[] props, NativeTypeInfo info, out int numProps)
	{
		EventInfo[] publicEvents = GetPublicEvents(type);
		int num = publicEvents.Length;
		int num2 = Marshal.SizeOf<ExtendPropertyData>();
		int num3 = props.Length;
		IntPtr intPtr = Marshal.AllocHGlobal((num3 + num) * num2);
		numProps = 0;
		if (num3 > 0)
		{
			bool usePropertyInfo = type.GetTypeInfo().IsValueType || Platform.ID == PlatformID.iPhone;
			NativeTypePropsInfo info2 = (NativeTypePropsInfo)info;
			for (int i = 0; i < num3; i++)
			{
				PropertyInfo propertyInfo = props[i];
				if (propertyInfo.GetGetMethod() != null && !propertyInfo.PropertyType.IsPointer && !propertyInfo.PropertyType.IsByRef && (HasTypeConverter(propertyInfo) || (!IsIndexerProperty(propertyInfo) && !IsDependencyProperty(type, propertyInfo))))
				{
					ExtendPropertyData extendPropertyData = AddProperty(info2, propertyInfo, usePropertyInfo);
					int num4 = numProps * num2;
					Marshal.WriteInt64(intPtr, num4, extendPropertyData.name);
					Marshal.WriteInt64(intPtr, num4 + 8, extendPropertyData.type);
					Marshal.WriteInt64(intPtr, num4 + 16, extendPropertyData.typeConverter);
					Marshal.WriteInt32(intPtr, num4 + 24, extendPropertyData.extendType);
					Marshal.WriteInt32(intPtr, num4 + 28, extendPropertyData.readOnly);
					numProps++;
				}
			}
		}
		if (num > 0)
		{
			for (int j = 0; j < num; j++)
			{
				EventInfo eventInfo = publicEvents[j];
				int num5 = numProps * num2;
				ExtendPropertyData extendPropertyData2 = new ExtendPropertyData
				{
					name = Marshal.StringToHGlobalAnsi(eventInfo.Name).ToInt64(),
					extendType = 43
				};
				Marshal.WriteInt64(intPtr, num5, extendPropertyData2.name);
				Marshal.WriteInt64(intPtr, num5 + 8, extendPropertyData2.type);
				Marshal.WriteInt64(intPtr, num5 + 16, extendPropertyData2.typeConverter);
				Marshal.WriteInt32(intPtr, num5 + 24, extendPropertyData2.extendType);
				Marshal.WriteInt32(intPtr, num5 + 28, extendPropertyData2.readOnly);
				numProps++;
			}
		}
		return intPtr;
	}

	private static IntPtr CreateNativeEnumsData(Type type, out int numEnums)
	{
		string[] names = Enum.GetNames(type);
		Array values = Enum.GetValues(type);
		numEnums = values.Length;
		int num = Marshal.SizeOf<long>() * 2;
		IntPtr intPtr = Marshal.AllocHGlobal(numEnums * num);
		for (int i = 0; i < numEnums; i++)
		{
			object value = values.GetValue(i);
			string s = (string)names.GetValue(i);
			int num2 = i * num;
			Marshal.WriteInt64(intPtr, num2, Marshal.StringToHGlobalAnsi(s).ToInt64());
			Marshal.WriteInt64(intPtr, num2 + 8, Convert.ToInt64(value));
		}
		return intPtr;
	}

	public static IntPtr EnsureNativeType(Type type)
	{
		return EnsureNativeType(type, registerDP: true);
	}

	public static IntPtr EnsureNativeType(Type type, bool registerDP)
	{
		if (type == null)
		{
			type = typeof(BaseComponent);
		}
		IntPtr intPtr = TryGetNativeType(type);
		if (intPtr == IntPtr.Zero)
		{
			intPtr = RegisterNativeType(type, registerDP);
		}
		if (intPtr == IntPtr.Zero)
		{
			throw new Exception(string.Format("Unable to register native type for '%s'", type.FullName));
		}
		return intPtr;
	}

	private static void RegisterDependencyProperties(Type type)
	{
		if (!(type.Assembly == BclAssembly) && (typeof(DependencyObject).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()) || HasDependencyProperties(type)))
		{
			RunClassConstructor(type);
		}
	}

	private static bool HasDependencyProperties(Type type)
	{
		FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (fields.Length != 0)
		{
			FieldInfo[] array = fields;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].FieldType == typeof(DependencyProperty))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void RunClassConstructor(Type type)
	{
		if (_constructedTypes.Add(type))
		{
			RuntimeHelpers.RunClassConstructor(type.TypeHandle);
		}
		else
		{
			DependencyPropertyRegistry.Restore(type);
		}
	}

	private static MethodInfo FindExtendMethod(Type type)
	{
		Type type2 = type;
		while (type2 != null)
		{
			MethodInfo extendMethod = GetExtendMethod(type2);
			if (extendMethod != null)
			{
				return extendMethod;
			}
			type2 = type2.GetTypeInfo().BaseType;
		}
		return null;
	}

	private static MethodInfo GetExtendMethod(Type type)
	{
		MethodInfo method = type.GetMethod("Extend", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic);
		if (method != null && method.GetParameters().Length == 1)
		{
			return method;
		}
		return null;
	}

	[MonoPInvokeCallback(typeof(Callback_FreeString))]
	private static void FreeString(IntPtr strPtr)
	{
		try
		{
			Marshal.FreeHGlobal(strPtr);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	private static int NextPowerOf2(int x)
	{
		if (x < 0)
		{
			return 0;
		}
		x--;
		x |= x >> 1;
		x |= x >> 2;
		x |= x >> 4;
		x |= x >> 8;
		x |= x >> 16;
		return x + 1;
	}

	public static string StringFromNativeUtf8(IntPtr nativeUtf8)
	{
		if (nativeUtf8 == IntPtr.Zero)
		{
			return string.Empty;
		}
		int i;
		for (i = 0; Marshal.ReadByte(nativeUtf8, i) != 0; i++)
		{
		}
		if (_byteBuffer == null || i > _byteBuffer.Length)
		{
			_byteBuffer = new byte[NextPowerOf2(Math.Max(2048, i))];
		}
		Marshal.Copy(nativeUtf8, _byteBuffer, 0, i);
		return Encoding.UTF8.GetString(_byteBuffer, 0, i);
	}

	[MonoPInvokeCallback(typeof(Callback_RegisterType))]
	private static void RegisterType(string typeName)
	{
		try
		{
			if (typeName == "NoesisApp.Application")
			{
				Debug.LogError("Application not supported in Unity, please set Application Resources in NoesisGUI settings.");
				return;
			}
			if (typeName == "NoesisApp.Window")
			{
				Debug.LogError("Window not supported in Unity, please use Page or UserControl instead.");
				return;
			}
			Type type = FindType(typeName);
			if (type != null)
			{
				RegisterNativeType(type);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	public static Type FindType(string name)
	{
		Type type = Type.GetType(name);
		if (type == null)
		{
			if (_assemblies == null)
			{
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				_assemblies = new List<Assembly>(assemblies.Length);
				_assemblies.AddRange(assemblies);
				AppDomain.CurrentDomain.AssemblyLoad += AddLoadedAssembly;
			}
			for (int num = _assemblies.Count - 1; num >= 0; num--)
			{
				type = _assemblies[num].GetType(name);
				if (type != null)
				{
					break;
				}
			}
		}
		return type;
	}

	private static void AddLoadedAssembly(object sender, AssemblyLoadEventArgs e)
	{
		_assemblies.Add(e.LoadedAssembly);
	}

	[MonoPInvokeCallback(typeof(Callback_ToString))]
	private static IntPtr ToStringEx(IntPtr cPtr)
	{
		try
		{
			object extendInstance = GetExtendInstance(cPtr);
			string text = ((extendInstance != null) ? extendInstance.ToString() : string.Empty);
			return Marshal.StringToHGlobalUni((text != null) ? text : string.Empty);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return Marshal.StringToHGlobalUni(string.Empty);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_Equals))]
	private static bool EqualsEx(IntPtr cPtr, IntPtr cPtrOtherType, IntPtr cPtrOther)
	{
		try
		{
			object extendInstance = GetExtendInstance(cPtr);
			object proxy = GetProxy(cPtrOtherType, cPtrOther, ownMemory: false);
			return object.Equals(extendInstance, proxy);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return false;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_VisualChildrenCount))]
	private static int VisualChildrenCount(IntPtr cPtr, Visual.ChildrenCountBaseCallback callback)
	{
		try
		{
			Visual visual = (Visual)GetExtendInstance(cPtr);
			if (visual != null)
			{
				visual.ChildrenCountBase = callback;
				int visualChildrenCount = visual.VisualChildrenCount;
				visual.ChildrenCountBase = null;
				return visualChildrenCount;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return 0;
	}

	[MonoPInvokeCallback(typeof(Callback_VisualGetChild))]
	private static IntPtr VisualGetChild(IntPtr cPtr, int index, Visual.ChildrenCountBaseCallback countCallback, Visual.GetChildBaseCallback childCallback)
	{
		try
		{
			Visual visual = (Visual)GetExtendInstance(cPtr);
			if (visual != null)
			{
				visual.ChildrenCountBase = countCallback;
				visual.GetChildBase = childCallback;
				Visual visualChild = visual.GetVisualChild(index);
				visual.GetChildBase = null;
				visual.ChildrenCountBase = null;
				HandleRef instanceHandle = GetInstanceHandle(visualChild);
				BaseComponent.AddReference(instanceHandle.Handle);
				return instanceHandle.Handle;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return IntPtr.Zero;
	}

	[MonoPInvokeCallback(typeof(Callback_UIElementRender))]
	private static void UIElementRender(IntPtr cPtr, IntPtr contextType, IntPtr context, UIElement.RenderBaseCallback callback)
	{
		try
		{
			UIElement uIElement = (UIElement)GetExtendInstance(cPtr);
			if (uIElement != null)
			{
				uIElement.OnRenderBase = callback;
				uIElement.OnRender((DrawingContext)GetProxy(contextType, context, ownMemory: false));
				uIElement.OnRenderBase = null;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_FrameworkElementConnectEvent))]
	private static bool FrameworkElementConnectEvent(IntPtr cPtr, IntPtr cPtrSourceType, IntPtr cPtrSource, string eventName, string handlerName)
	{
		try
		{
			FrameworkElement frameworkElement = (FrameworkElement)GetExtendInstance(cPtr);
			if (frameworkElement != null)
			{
				object proxy = GetProxy(cPtrSourceType, cPtrSource, ownMemory: false);
				return frameworkElement.ConnectEvent(proxy, eventName, handlerName);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_FrameworkElementConnectField))]
	private static void FrameworkElementConnectField(IntPtr cPtr, IntPtr cPtrSourceType, IntPtr cPtrSource, string fieldName)
	{
		try
		{
			FrameworkElement frameworkElement = (FrameworkElement)GetExtendInstance(cPtr);
			if (frameworkElement != null)
			{
				object proxy = GetProxy(cPtrSourceType, cPtrSource, ownMemory: false);
				frameworkElement.ConnectField(proxy, fieldName);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_FrameworkElementMeasure))]
	private static void FrameworkElementMeasure(IntPtr cPtr, ref Size availableSize, ref Size desiredSize, FrameworkElement.LayoutBaseCallback callback)
	{
		try
		{
			FrameworkElement frameworkElement = (FrameworkElement)GetExtendInstance(cPtr);
			if (frameworkElement != null)
			{
				frameworkElement.MeasureBase = callback;
				desiredSize = frameworkElement.MeasureOverride(availableSize);
				frameworkElement.MeasureBase = null;
				if (desiredSize.IsEmpty)
				{
					desiredSize = new Size(0f, 0f);
				}
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_FrameworkElementArrange))]
	private static void FrameworkElementArrange(IntPtr cPtr, ref Size finalSize, ref Size renderSize, FrameworkElement.LayoutBaseCallback callback)
	{
		try
		{
			FrameworkElement frameworkElement = (FrameworkElement)GetExtendInstance(cPtr);
			if (frameworkElement != null)
			{
				frameworkElement.ArrangeBase = callback;
				renderSize = frameworkElement.ArrangeOverride(finalSize);
				frameworkElement.ArrangeBase = callback;
				if (renderSize.IsEmpty)
				{
					renderSize = new Size(0f, 0f);
				}
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_FrameworkElementApplyTemplate))]
	private static void FrameworkElementApplyTemplate(IntPtr cPtr)
	{
		try
		{
			FrameworkElement frameworkElement = (FrameworkElement)GetExtendInstance(cPtr);
			if (frameworkElement != null)
			{
				frameworkElement.OnApplyTemplate();
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ItemsControlGetContainer))]
	private static IntPtr ItemsControlGetContainer(IntPtr cPtr, ItemsControl.GetContainerForItemBaseCallback callback)
	{
		try
		{
			ItemsControl itemsControl = (ItemsControl)GetExtendInstance(cPtr);
			if (itemsControl != null)
			{
				itemsControl.GetContainerForItemBase = callback;
				DependencyObject containerForItemOverride = itemsControl.GetContainerForItemOverride();
				itemsControl.GetContainerForItemBase = null;
				HandleRef instanceHandle = GetInstanceHandle(containerForItemOverride);
				BaseComponent.AddReference(instanceHandle.Handle);
				return instanceHandle.Handle;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return IntPtr.Zero;
	}

	[MonoPInvokeCallback(typeof(Callback_ItemsControlIsContainer))]
	private static bool ItemsControlIsContainer(IntPtr cPtr, IntPtr itemTypePtr, IntPtr itemPtr, ItemsControl.IsItemItsOwnContainerBaseCallback callback)
	{
		try
		{
			ItemsControl itemsControl = (ItemsControl)GetExtendInstance(cPtr);
			if (itemsControl != null)
			{
				itemsControl.IsItemItsOwnContainerBase = callback;
				bool result = itemsControl.IsItemItsOwnContainerOverride(GetProxy(itemTypePtr, itemPtr, ownMemory: false));
				itemsControl.IsItemItsOwnContainerBase = null;
				return result;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_AdornerGetTransform))]
	private static void AdornerGetTransform(IntPtr cPtr, ref Matrix4 transform, ref Matrix4 desiredTransform)
	{
		try
		{
			Adorner adorner = (Adorner)GetExtendInstance(cPtr);
			if (adorner != null)
			{
				desiredTransform = adorner.GetDesiredTransform(transform);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_FreezableClone))]
	private static void FreezableClone(IntPtr cPtrType, IntPtr cPtrClone, IntPtr cPtrSource)
	{
		try
		{
			Freezable freezable = (Freezable)GetExtendInstance(cPtrClone);
			Freezable freezable2 = (Freezable)GetExtendInstance(cPtrSource);
			if (freezable != null && freezable2 != null)
			{
				freezable.CallCloneCommonCore(freezable2);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ShapeGetGeometry))]
	private static IntPtr ShapeGetGeometry(IntPtr cPtr)
	{
		try
		{
			return GetInstanceHandle(((Shape)GetExtendInstance(cPtr)).DefiningGeometry).Handle;
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return IntPtr.Zero;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_CommandCanExecute))]
	private static bool CommandCanExecute(IntPtr cPtr, IntPtr paramType, IntPtr paramPtr)
	{
		try
		{
			return ((ICommand)GetExtendInstance(cPtr))?.CanExecute(GetProxy(paramType, paramPtr, ownMemory: false)) ?? false;
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return false;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_CommandExecute))]
	private static void CommandExecute(IntPtr cPtr, IntPtr paramType, IntPtr paramPtr)
	{
		try
		{
			((ICommand)GetExtendInstance(cPtr))?.Execute(GetProxy(paramType, paramPtr, ownMemory: false));
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	private static bool IsNullableType(Type type)
	{
		if (type.GetTypeInfo().IsGenericType)
		{
			return type.GetTypeInfo().GetGenericTypeDefinition().Equals(typeof(Nullable<>));
		}
		return false;
	}

	private static bool AreCompatibleTypes(object source, Type targetType)
	{
		if (source == DependencyProperty.UnsetValue || source == Binding.DoNothing)
		{
			return true;
		}
		if (source == null)
		{
			if (targetType.GetTypeInfo().IsValueType)
			{
				return IsNullableType(targetType);
			}
			return true;
		}
		Type type = source.GetType();
		if (targetType.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			return true;
		}
		if (targetType.Equals(typeof(BaseComponent)))
		{
			return true;
		}
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_ConverterConvert))]
	private static bool ConverterConvert(IntPtr cPtr, IntPtr valType, IntPtr valPtr, IntPtr targetTypePtr, IntPtr paramType, IntPtr paramPtr, out IntPtr result)
	{
		try
		{
			IValueConverter valueConverter = (IValueConverter)GetExtendInstance(cPtr);
			if (valueConverter != null)
			{
				Type type = GetNativeTypeInfo(targetTypePtr).Type;
				object proxy = GetProxy(valType, valPtr, ownMemory: false);
				object proxy2 = GetProxy(paramType, paramPtr, ownMemory: false);
				object obj = valueConverter.Convert(proxy, type, proxy2, CultureInfo.CurrentCulture);
				if (AreCompatibleTypes(obj, type))
				{
					HandleRef instanceHandle = GetInstanceHandle(obj);
					BaseComponent.AddReference(instanceHandle.Handle);
					result = instanceHandle.Handle;
					return true;
				}
				Log.Error(string.Format("{0} Convert() expects {1} and {2} is returned", valueConverter.GetType().FullName, type.FullName, (obj != null) ? obj.GetType().FullName : "null"));
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		result = IntPtr.Zero;
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_ConverterConvertBack))]
	private static bool ConverterConvertBack(IntPtr cPtr, IntPtr valType, IntPtr valPtr, IntPtr targetTypePtr, IntPtr paramType, IntPtr paramPtr, out IntPtr result)
	{
		try
		{
			IValueConverter valueConverter = (IValueConverter)GetExtendInstance(cPtr);
			if (valueConverter != null)
			{
				Type type = GetNativeTypeInfo(targetTypePtr).Type;
				object proxy = GetProxy(valType, valPtr, ownMemory: false);
				object proxy2 = GetProxy(paramType, paramPtr, ownMemory: false);
				object obj = valueConverter.ConvertBack(proxy, type, proxy2, CultureInfo.CurrentCulture);
				if (AreCompatibleTypes(obj, type))
				{
					HandleRef instanceHandle = GetInstanceHandle(obj);
					BaseComponent.AddReference(instanceHandle.Handle);
					result = instanceHandle.Handle;
					return true;
				}
				Log.Error(string.Format("{0} ConvertBack() expects {1} and {2} is returned", valueConverter.GetType().FullName, type.FullName, (obj != null) ? obj.GetType().FullName : "null"));
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		result = IntPtr.Zero;
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_MultiConverterConvert))]
	private static bool MultiConverterConvert(IntPtr cPtr, int numSources, IntPtr valTypes, IntPtr valPtrs, IntPtr targetTypePtr, IntPtr paramType, IntPtr paramPtr, out IntPtr result)
	{
		try
		{
			IMultiValueConverter multiValueConverter = (IMultiValueConverter)GetExtendInstance(cPtr);
			if (multiValueConverter != null)
			{
				int num = Marshal.SizeOf<IntPtr>();
				object[] array = new object[numSources];
				for (int i = 0; i < numSources; i++)
				{
					IntPtr nativeType = Marshal.ReadIntPtr(valTypes, i * num);
					IntPtr cPtr2 = Marshal.ReadIntPtr(valPtrs, i * num);
					array[i] = GetProxy(nativeType, cPtr2, ownMemory: false);
				}
				Type type = GetNativeTypeInfo(targetTypePtr).Type;
				object proxy = GetProxy(paramType, paramPtr, ownMemory: false);
				object obj = multiValueConverter.Convert(array, type, proxy, CultureInfo.CurrentCulture);
				if (AreCompatibleTypes(obj, type))
				{
					HandleRef instanceHandle = GetInstanceHandle(obj);
					BaseComponent.AddReference(instanceHandle.Handle);
					result = instanceHandle.Handle;
					return true;
				}
				Log.Error(string.Format("{0} Convert() expects {1} and {2} is returned", multiValueConverter.GetType().FullName, type.FullName, (obj != null) ? obj.GetType().FullName : "null"));
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		result = IntPtr.Zero;
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_MultiConverterConvertBack))]
	private static bool MultiConverterConvertBack(IntPtr cPtr, int numSources, IntPtr valType, IntPtr valPtr, IntPtr targetTypePtrs, IntPtr paramType, IntPtr paramPtr, IntPtr results)
	{
		try
		{
			IMultiValueConverter multiValueConverter = (IMultiValueConverter)GetExtendInstance(cPtr);
			if (multiValueConverter != null)
			{
				int num = Marshal.SizeOf<IntPtr>();
				Type[] array = new Type[numSources];
				for (int i = 0; i < numSources; i++)
				{
					IntPtr nativeType = Marshal.ReadIntPtr(targetTypePtrs, i * num);
					array[i] = GetNativeTypeInfo(nativeType).Type;
				}
				object proxy = GetProxy(valType, valPtr, ownMemory: false);
				object proxy2 = GetProxy(paramType, paramPtr, ownMemory: false);
				object[] array2 = multiValueConverter.ConvertBack(proxy, array, proxy2, CultureInfo.CurrentCulture);
				if (array2 != null)
				{
					for (int j = 0; j < numSources; j++)
					{
						HandleRef instanceHandle = GetInstanceHandle((j < array2.Length) ? array2[j] : null);
						BaseComponent.AddReference(instanceHandle.Handle);
						Marshal.WriteIntPtr(results, j * num, instanceHandle.Handle);
					}
					return true;
				}
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_ListCount))]
	private static uint ListCount(IntPtr cPtr)
	{
		try
		{
			IList list = (IList)GetExtendInstance(cPtr);
			return (list != null && Initialized) ? ((uint)list.Count) : 0u;
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0u;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ListGet))]
	private static IntPtr ListGet(IntPtr cPtr, uint index)
	{
		try
		{
			HandleRef instanceHandle = GetInstanceHandle(((IList)GetExtendInstance(cPtr))?[(int)index]);
			BaseComponent.AddReference(instanceHandle.Handle);
			return instanceHandle.Handle;
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return IntPtr.Zero;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ListSet))]
	private static void ListSet(IntPtr cPtr, uint index, IntPtr itemType, IntPtr item)
	{
		try
		{
			IList list = (IList)GetExtendInstance(cPtr);
			if (list != null)
			{
				list[(int)index] = GetProxy(itemType, item, ownMemory: false);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ListAdd))]
	private static uint ListAdd(IntPtr cPtr, IntPtr itemType, IntPtr item)
	{
		try
		{
			return (uint)(((IList)GetExtendInstance(cPtr))?.Add(GetProxy(itemType, item, ownMemory: false)) ?? 0);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0u;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ListInsert))]
	private static void ListInsert(IntPtr cPtr, uint index, IntPtr itemType, IntPtr item)
	{
		try
		{
			((IList)GetExtendInstance(cPtr))?.Insert((int)index, GetProxy(itemType, item, ownMemory: false));
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ListIndexOf))]
	private static int ListIndexOf(IntPtr cPtr, IntPtr itemType, IntPtr item)
	{
		try
		{
			return ((IList)GetExtendInstance(cPtr))?.IndexOf(GetProxy(itemType, item, ownMemory: false)) ?? (-1);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return -1;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ListRemoveAt))]
	private static void ListRemoveAt(IntPtr cPtr, uint index)
	{
		try
		{
			((IList)GetExtendInstance(cPtr))?.RemoveAt((int)index);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ListClear))]
	private static void ListClear(IntPtr cPtr)
	{
		try
		{
			((IList)GetExtendInstance(cPtr))?.Clear();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_DictionaryFind))]
	private static bool DictionaryFind(IntPtr cPtr, string key, ref IntPtr item)
	{
		try
		{
			IDictionary dictionary = (IDictionary)GetExtendInstance(cPtr);
			if (dictionary != null && dictionary.Contains(key))
			{
				HandleRef instanceHandle = GetInstanceHandle(dictionary[key]);
				BaseComponent.AddReference(instanceHandle.Handle);
				item = instanceHandle.Handle;
				return true;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		item = IntPtr.Zero;
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_DictionarySet))]
	private static void DictionarySet(IntPtr cPtr, string key, IntPtr itemType, IntPtr item)
	{
		try
		{
			IDictionary dictionary = (IDictionary)GetExtendInstance(cPtr);
			if (dictionary != null)
			{
				dictionary[key] = GetProxy(itemType, item, ownMemory: false);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_DictionaryAdd))]
	private static void DictionaryAdd(IntPtr cPtr, string key, IntPtr itemType, IntPtr item)
	{
		try
		{
			((IDictionary)GetExtendInstance(cPtr))?.Add(key, GetProxy(itemType, item, ownMemory: false));
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_DictionaryRemove))]
	private static void DictionaryRemove(IntPtr cPtr, string key)
	{
		try
		{
			((IDictionary)GetExtendInstance(cPtr))?.Remove(key);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ListIndexerTryGet))]
	[return: MarshalAs(UnmanagedType.U1)]
	private static bool ListIndexerTryGet(IntPtr cPtrType, IntPtr cPtr, uint index, ref IntPtr item)
	{
		try
		{
			object extendInstance = GetExtendInstance(cPtr);
			if (extendInstance != null)
			{
				NativeTypeIndexerInfo nativeTypeIndexerInfo = (NativeTypeIndexerInfo)GetNativeTypeInfo(cPtrType);
				try
				{
					HandleRef instanceHandle = GetInstanceHandle(((IndexerAccessorT<int>)nativeTypeIndexerInfo.Indexer).Get(extendInstance, (int)index));
					BaseComponent.AddReference(instanceHandle.Handle);
					item = instanceHandle.Handle;
					return true;
				}
				catch
				{
				}
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		item = IntPtr.Zero;
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_ListIndexerTrySet))]
	[return: MarshalAs(UnmanagedType.U1)]
	private static bool ListIndexerTrySet(IntPtr cPtrType, IntPtr cPtr, uint index, IntPtr itemType, IntPtr item)
	{
		try
		{
			object extendInstance = GetExtendInstance(cPtr);
			if (extendInstance != null)
			{
				object proxy = GetProxy(itemType, item, ownMemory: false);
				NativeTypeIndexerInfo nativeTypeIndexerInfo = (NativeTypeIndexerInfo)GetNativeTypeInfo(cPtrType);
				try
				{
					((IndexerAccessorT<int>)nativeTypeIndexerInfo.Indexer).Set(extendInstance, (int)index, proxy);
					return true;
				}
				catch
				{
				}
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_DictionaryIndexerTryGet))]
	[return: MarshalAs(UnmanagedType.U1)]
	private static bool DictionaryIndexerTryGet(IntPtr cPtrType, IntPtr cPtr, string key, ref IntPtr item)
	{
		try
		{
			object extendInstance = GetExtendInstance(cPtr);
			if (extendInstance != null)
			{
				NativeTypeIndexerInfo nativeTypeIndexerInfo = (NativeTypeIndexerInfo)GetNativeTypeInfo(cPtrType);
				try
				{
					HandleRef instanceHandle = GetInstanceHandle(((IndexerAccessorT<string>)nativeTypeIndexerInfo.Indexer).Get(extendInstance, key));
					BaseComponent.AddReference(instanceHandle.Handle);
					item = instanceHandle.Handle;
					return true;
				}
				catch
				{
				}
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		item = IntPtr.Zero;
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_DictionaryIndexerTrySet))]
	[return: MarshalAs(UnmanagedType.U1)]
	private static bool DictionaryIndexerTrySet(IntPtr cPtrType, IntPtr cPtr, string key, IntPtr itemType, IntPtr item)
	{
		try
		{
			object extendInstance = GetExtendInstance(cPtr);
			if (extendInstance != null)
			{
				object proxy = GetProxy(itemType, item, ownMemory: false);
				NativeTypeIndexerInfo nativeTypeIndexerInfo = (NativeTypeIndexerInfo)GetNativeTypeInfo(cPtrType);
				try
				{
					((IndexerAccessorT<string>)nativeTypeIndexerInfo.Indexer).Set(extendInstance, key, proxy);
					return true;
				}
				catch
				{
				}
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_SelectTemplate))]
	private static IntPtr SelectTemplate(IntPtr cPtr, IntPtr itemType, IntPtr item, IntPtr containerType, IntPtr container)
	{
		try
		{
			DataTemplateSelector dataTemplateSelector = (DataTemplateSelector)GetExtendInstance(cPtr);
			if (dataTemplateSelector != null)
			{
				return GetInstanceHandle(dataTemplateSelector.SelectTemplate(GetProxy(itemType, item, ownMemory: false), (DependencyObject)GetProxy(containerType, container, ownMemory: false))).Handle;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return IntPtr.Zero;
	}

	[MonoPInvokeCallback(typeof(Callback_StreamSetPosition))]
	private static void StreamSetPosition(IntPtr cPtr, uint pos)
	{
		try
		{
			Stream stream = (Stream)GetExtendInstance(cPtr);
			if (stream != null)
			{
				stream.Position = pos;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_StreamGetPosition))]
	private static uint StreamGetPosition(IntPtr cPtr)
	{
		try
		{
			Stream stream = (Stream)GetExtendInstance(cPtr);
			return (uint)((stream != null) ? stream.Position : 0u);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0u;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_StreamGetLength))]
	private static uint StreamGetLength(IntPtr cPtr)
	{
		try
		{
			Stream stream = (Stream)GetExtendInstance(cPtr);
			return (uint)((stream != null) ? stream.Length : 0u);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0u;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_StreamRead))]
	private static uint StreamRead(IntPtr cPtr, IntPtr buffer, uint bufferSize)
	{
		try
		{
			Stream stream = (Stream)GetExtendInstance(cPtr);
			if (stream != null)
			{
				if (_byteBuffer == null || (int)bufferSize > _byteBuffer.Length)
				{
					_byteBuffer = new byte[NextPowerOf2(Math.Max(2048, (int)bufferSize))];
				}
				int result = stream.Read(_byteBuffer, 0, (int)bufferSize);
				System.Runtime.InteropServices.Marshal.Copy(_byteBuffer, 0, buffer, (int)bufferSize);
				return (uint)result;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return 0u;
	}

	[MonoPInvokeCallback(typeof(Callback_StreamClose))]
	private static void StreamClose(IntPtr cPtr)
	{
		try
		{
			((Stream)GetExtendInstance(cPtr))?.Close();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ProviderLoadXaml))]
	private static IntPtr ProviderLoadXaml(IntPtr cPtr, IntPtr filename)
	{
		try
		{
			XamlProvider xamlProvider = (XamlProvider)GetExtendInstance(cPtr);
			if (xamlProvider != null)
			{
				Uri uri = new Uri(StringFromNativeUtf8(filename), UriKind.RelativeOrAbsolute);
				HandleRef instanceHandle = GetInstanceHandle(xamlProvider.LoadXaml(uri));
				BaseComponent.AddReference(instanceHandle.Handle);
				return instanceHandle.Handle;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return IntPtr.Zero;
	}

	[MonoPInvokeCallback(typeof(Callback_ProviderTextureInfo))]
	private static void ProviderTextureInfo(IntPtr cPtr, IntPtr filename, ref uint x, ref uint y, ref uint width, ref uint height, ref float dpiScale)
	{
		width = (height = 0u);
		try
		{
			TextureProvider textureProvider = (TextureProvider)GetExtendInstance(cPtr);
			if (textureProvider != null)
			{
				Uri uri = new Uri(StringFromNativeUtf8(filename), UriKind.RelativeOrAbsolute);
				TextureProvider.TextureInfo textureInfo = textureProvider.GetTextureInfo(uri);
				x = (uint)textureInfo.Rect.X;
				y = (uint)textureInfo.Rect.Y;
				width = (uint)textureInfo.Rect.Width;
				height = (uint)textureInfo.Rect.Height;
				dpiScale = textureInfo.DpiScale;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ProviderTextureLoad))]
	private static IntPtr ProviderTextureLoad(IntPtr cPtr, IntPtr filename)
	{
		try
		{
			TextureProvider textureProvider = (TextureProvider)GetExtendInstance(cPtr);
			if (textureProvider != null)
			{
				Uri uri = new Uri(StringFromNativeUtf8(filename), UriKind.RelativeOrAbsolute);
				HandleRef instanceHandle = GetInstanceHandle(textureProvider.LoadTexture(uri));
				BaseComponent.AddReference(instanceHandle.Handle);
				return instanceHandle.Handle;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return IntPtr.Zero;
	}

	[MonoPInvokeCallback(typeof(Callback_ProviderTextureOpen))]
	private static IntPtr ProviderTextureOpen(IntPtr cPtr, IntPtr filename)
	{
		try
		{
			FileTextureProvider fileTextureProvider = (FileTextureProvider)GetExtendInstance(cPtr);
			if (fileTextureProvider != null)
			{
				Uri filename2 = new Uri(StringFromNativeUtf8(filename), UriKind.RelativeOrAbsolute);
				HandleRef instanceHandle = GetInstanceHandle(fileTextureProvider.OpenStream(filename2));
				BaseComponent.AddReference(instanceHandle.Handle);
				return instanceHandle.Handle;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return IntPtr.Zero;
	}

	[MonoPInvokeCallback(typeof(Callback_ProviderMatchFont))]
	private static IntPtr ProviderMatchFont(IntPtr cPtr, IntPtr baseUri, IntPtr familyName, ref int weight, ref int stretch, ref int style, ref uint index)
	{
		try
		{
			FontProvider fontProvider = (FontProvider)GetExtendInstance(cPtr);
			if (fontProvider != null)
			{
				Uri baseUri2 = new Uri(StringFromNativeUtf8(baseUri), UriKind.RelativeOrAbsolute);
				string familyName2 = StringFromNativeUtf8(familyName);
				FontWeight weight2 = (FontWeight)weight;
				FontStretch stretch2 = (FontStretch)stretch;
				FontStyle style2 = (FontStyle)style;
				FontProvider.FontSource fontSource = fontProvider.MatchFont(baseUri2, familyName2, ref weight2, ref stretch2, ref style2);
				weight = (int)weight2;
				stretch = (int)stretch2;
				style = (int)style2;
				index = fontSource.faceIndex;
				HandleRef instanceHandle = GetInstanceHandle(fontSource.file);
				BaseComponent.AddReference(instanceHandle.Handle);
				return instanceHandle.Handle;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return IntPtr.Zero;
	}

	[MonoPInvokeCallback(typeof(Callback_ProviderFamilyExists))]
	[return: MarshalAs(UnmanagedType.U1)]
	private static bool ProviderFamilyExists(IntPtr cPtr, IntPtr baseUri, IntPtr familyName)
	{
		try
		{
			FontProvider fontProvider = (FontProvider)GetExtendInstance(cPtr);
			if (fontProvider != null)
			{
				Uri baseUri2 = new Uri(StringFromNativeUtf8(baseUri), UriKind.RelativeOrAbsolute);
				string familyName2 = StringFromNativeUtf8(familyName);
				return fontProvider.FamilyExists(baseUri2, familyName2);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_ProviderScanFolder))]
	private static void ProviderScanFolder(IntPtr cPtr, IntPtr folder)
	{
		try
		{
			FontProvider fontProvider = (FontProvider)GetExtendInstance(cPtr);
			if (fontProvider != null)
			{
				Uri folder2 = new Uri(StringFromNativeUtf8(folder), UriKind.RelativeOrAbsolute);
				fontProvider.ScanFolder(folder2);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ProviderOpenFont))]
	private static IntPtr ProviderOpenFont(IntPtr cPtr, IntPtr folder, IntPtr filename)
	{
		try
		{
			FontProvider fontProvider = (FontProvider)GetExtendInstance(cPtr);
			if (fontProvider != null)
			{
				string uriString = StringFromNativeUtf8(folder);
				string filename2 = StringFromNativeUtf8(filename);
				Uri folder2 = new Uri(uriString, UriKind.RelativeOrAbsolute);
				HandleRef instanceHandle = GetInstanceHandle(fontProvider.OpenFont(folder2, filename2));
				BaseComponent.AddReference(instanceHandle.Handle);
				return instanceHandle.Handle;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return IntPtr.Zero;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoBringIntoView))]
	private static void ScrollInfoBringIntoView(IntPtr cPtr, int index)
	{
		try
		{
			VirtualizingPanel virtualizingPanel = (VirtualizingPanel)GetExtendInstance(cPtr);
			if (virtualizingPanel != null)
			{
				virtualizingPanel.BringIndexIntoViewPublic(index);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoGetCanHorizontalScroll))]
	[return: MarshalAs(UnmanagedType.U1)]
	private static bool ScrollInfoGetCanHorizontalScroll(IntPtr cPtr)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				return scrollInfo.CanHorizontallyScroll;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoSetCanHorizontalScroll))]
	private static void ScrollInfoSetCanHorizontalScroll(IntPtr cPtr, bool canScroll)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				scrollInfo.CanHorizontallyScroll = canScroll;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoGetCanVerticalScroll))]
	[return: MarshalAs(UnmanagedType.U1)]
	private static bool ScrollInfoGetCanVerticalScroll(IntPtr cPtr)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				return scrollInfo.CanVerticallyScroll;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return false;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoSetCanVerticalScroll))]
	private static void ScrollInfoSetCanVerticalScroll(IntPtr cPtr, bool canScroll)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				scrollInfo.CanVerticallyScroll = canScroll;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoGetExtentWidth))]
	private static float ScrollInfoGetExtentWidth(IntPtr cPtr)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				return scrollInfo.ExtentWidth;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return 0f;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoGetExtentHeight))]
	private static float ScrollInfoGetExtentHeight(IntPtr cPtr)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				return scrollInfo.ExtentHeight;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return 0f;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoGetViewportWidth))]
	private static float ScrollInfoGetViewportWidth(IntPtr cPtr)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				return scrollInfo.ViewportWidth;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return 0f;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoGetViewportHeight))]
	private static float ScrollInfoGetViewportHeight(IntPtr cPtr)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				return scrollInfo.ViewportHeight;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return 0f;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoGetHorizontalOffset))]
	private static float ScrollInfoGetHorizontalOffset(IntPtr cPtr)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				return scrollInfo.HorizontalOffset;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return 0f;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoGetVerticalOffset))]
	private static float ScrollInfoGetVerticalOffset(IntPtr cPtr)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				return scrollInfo.VerticalOffset;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return 0f;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoGetScrollOwner))]
	private static IntPtr ScrollInfoGetScrollOwner(IntPtr cPtr)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				return GetInstanceHandle(scrollInfo.ScrollOwner).Handle;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
		return IntPtr.Zero;
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoSetScrollOwner))]
	private static void ScrollInfoSetScrollOwner(IntPtr cPtr, IntPtr typeOwner, IntPtr cPtrOwner)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				ScrollViewer scrollOwner = (ScrollViewer)GetProxy(typeOwner, cPtrOwner, ownMemory: false);
				scrollInfo.ScrollOwner = scrollOwner;
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoLineLeft))]
	private static void ScrollInfoLineLeft(IntPtr cPtr)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.LineLeft();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoLineRight))]
	private static void ScrollInfoLineRight(IntPtr cPtr)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.LineRight();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoLineUp))]
	private static void ScrollInfoLineUp(IntPtr cPtr)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.LineUp();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoLineDown))]
	private static void ScrollInfoLineDown(IntPtr cPtr)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.LineDown();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoPageLeft))]
	private static void ScrollInfoPageLeft(IntPtr cPtr)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.PageLeft();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoPageRight))]
	private static void ScrollInfoPageRight(IntPtr cPtr)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.PageRight();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoPageUp))]
	private static void ScrollInfoPageUp(IntPtr cPtr)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.PageUp();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoPageDown))]
	private static void ScrollInfoPageDown(IntPtr cPtr)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.PageDown();
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoMouseWheelLeft))]
	private static void ScrollInfoMouseWheelLeft(IntPtr cPtr, float delta)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.MouseWheelLeft(delta);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoMouseWheelRight))]
	private static void ScrollInfoMouseWheelRight(IntPtr cPtr, float delta)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.MouseWheelRight(delta);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoMouseWheelUp))]
	private static void ScrollInfoMouseWheelUp(IntPtr cPtr, float delta)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.MouseWheelUp(delta);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoMouseWheelDown))]
	private static void ScrollInfoMouseWheelDown(IntPtr cPtr, float delta)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.MouseWheelDown(delta);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoSetHorizontalOffset))]
	private static void ScrollInfoSetHorizontalOffset(IntPtr cPtr, float offset)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.SetHorizontalOffset(offset);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoSetVerticalOffset))]
	private static void ScrollInfoSetVerticalOffset(IntPtr cPtr, float offset)
	{
		try
		{
			((IScrollInfo)GetExtendInstance(cPtr))?.SetVerticalOffset(offset);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ScrollInfoMakeVisible))]
	private static void ScrollInfoMakeVisible(IntPtr cPtr, IntPtr visualType, IntPtr visualPtr, ref Rect rectangle, ref Rect result)
	{
		try
		{
			IScrollInfo scrollInfo = (IScrollInfo)GetExtendInstance(cPtr);
			if (scrollInfo != null)
			{
				Visual visual = (Visual)GetProxy(visualType, visualPtr, ownMemory: false);
				result = scrollInfo.MakeVisible(visual, rectangle);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_MarkupExtensionProvideValue))]
	private static IntPtr MarkupExtensionProvideValue(IntPtr cPtr, IntPtr provider)
	{
		try
		{
			MarkupExtension markupExtension = (MarkupExtension)GetExtendInstance(cPtr);
			MarkupExtensionProvider serviceProvider = new MarkupExtensionProvider(provider);
			HandleRef instanceHandle = GetInstanceHandle((markupExtension != null) ? markupExtension.ProvideValue(serviceProvider) : null);
			BaseComponent.AddReference(instanceHandle.Handle);
			return instanceHandle.Handle;
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return IntPtr.Zero;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_AnimationTimelineGetType))]
	private static IntPtr AnimationTimelineGetType(IntPtr cPtr)
	{
		try
		{
			return EnsureNativeType(((AnimationTimeline)GetExtendInstance(cPtr)).TargetPropertyType);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return IntPtr.Zero;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_AnimationTimelineGetValue))]
	private static IntPtr AnimationTimelineGetValue(IntPtr cPtr, IntPtr srcType, IntPtr srcPtr, IntPtr dstType, IntPtr dstPtr, IntPtr clockType, IntPtr clockPtr)
	{
		try
		{
			AnimationTimeline obj = (AnimationTimeline)GetExtendInstance(cPtr);
			object proxy = GetProxy(srcType, srcPtr, ownMemory: false);
			object proxy2 = GetProxy(dstType, dstPtr, ownMemory: false);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			HandleRef instanceHandle = GetInstanceHandle(obj.GetCurrentValue(proxy, proxy2, animationClock));
			BaseComponent.AddReference(instanceHandle.Handle);
			return instanceHandle.Handle;
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return IntPtr.Zero;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_BooleanAnimationGetValue))]
	[return: MarshalAs(UnmanagedType.U1)]
	private static bool BooleanAnimationGetValue(IntPtr cPtr, bool src, bool dst, IntPtr clockType, IntPtr clockPtr)
	{
		try
		{
			BooleanAnimationBase obj = (BooleanAnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			return obj.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return false;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_Int16AnimationGetValue))]
	private static short Int16AnimationGetValue(IntPtr cPtr, short src, short dst, IntPtr clockType, IntPtr clockPtr)
	{
		try
		{
			Int16AnimationBase obj = (Int16AnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			return obj.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_Int32AnimationGetValue))]
	private static int Int32AnimationGetValue(IntPtr cPtr, int src, int dst, IntPtr clockType, IntPtr clockPtr)
	{
		try
		{
			Int32AnimationBase obj = (Int32AnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			return obj.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_Int64AnimationGetValue))]
	private static long Int64AnimationGetValue(IntPtr cPtr, long src, long dst, IntPtr clockType, IntPtr clockPtr)
	{
		try
		{
			Int64AnimationBase obj = (Int64AnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			return obj.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0L;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_DoubleAnimationGetValue))]
	private static float DoubleAnimationGetValue(IntPtr cPtr, float src, float dst, IntPtr clockType, IntPtr clockPtr)
	{
		try
		{
			DoubleAnimationBase obj = (DoubleAnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			return obj.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0f;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ColorAnimationGetValue))]
	private static void ColorAnimationGetValue(IntPtr cPtr, ref Color src, ref Color dst, IntPtr clockType, IntPtr clockPtr, ref Color value)
	{
		try
		{
			ColorAnimationBase colorAnimationBase = (ColorAnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			value = colorAnimationBase.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_PointAnimationGetValue))]
	private static void PointAnimationGetValue(IntPtr cPtr, ref Point src, ref Point dst, IntPtr clockType, IntPtr clockPtr, ref Point value)
	{
		try
		{
			PointAnimationBase pointAnimationBase = (PointAnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			value = pointAnimationBase.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_RectAnimationGetValue))]
	private static void RectAnimationGetValue(IntPtr cPtr, ref Rect src, ref Rect dst, IntPtr clockType, IntPtr clockPtr, ref Rect value)
	{
		try
		{
			RectAnimationBase rectAnimationBase = (RectAnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			value = rectAnimationBase.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SizeAnimationGetValue))]
	private static void SizeAnimationGetValue(IntPtr cPtr, ref Size src, ref Size dst, IntPtr clockType, IntPtr clockPtr, ref Size value)
	{
		try
		{
			SizeAnimationBase sizeAnimationBase = (SizeAnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			value = sizeAnimationBase.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ThicknessAnimationGetValue))]
	private static void ThicknessAnimationGetValue(IntPtr cPtr, ref Thickness src, ref Thickness dst, IntPtr clockType, IntPtr clockPtr, ref Thickness value)
	{
		try
		{
			ThicknessAnimationBase thicknessAnimationBase = (ThicknessAnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			value = thicknessAnimationBase.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_ObjectAnimationGetValue))]
	private static IntPtr ObjectAnimationGetValue(IntPtr cPtr, IntPtr srcType, IntPtr srcPtr, IntPtr dstType, IntPtr dstPtr, IntPtr clockType, IntPtr clockPtr)
	{
		try
		{
			ObjectAnimationBase obj = (ObjectAnimationBase)GetExtendInstance(cPtr);
			object proxy = GetProxy(srcType, srcPtr, ownMemory: false);
			object proxy2 = GetProxy(dstType, dstPtr, ownMemory: false);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			HandleRef instanceHandle = GetInstanceHandle(obj.GetCurrentValueCore(proxy, proxy2, animationClock));
			BaseComponent.AddReference(instanceHandle.Handle);
			return instanceHandle.Handle;
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return IntPtr.Zero;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_StringAnimationGetValue))]
	private static IntPtr StringAnimationGetValue(IntPtr cPtr, IntPtr srcPtr, IntPtr dstPtr, IntPtr clockType, IntPtr clockPtr)
	{
		try
		{
			StringAnimationBase obj = (StringAnimationBase)GetExtendInstance(cPtr);
			string defaultOriginValue = StringFromNativeUtf8(srcPtr);
			string defaultDestinationValue = StringFromNativeUtf8(dstPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			string currentValueCore = obj.GetCurrentValueCore(defaultOriginValue, defaultDestinationValue, animationClock);
			return Marshal.StringToHGlobalUni((currentValueCore != null) ? currentValueCore : string.Empty);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return IntPtr.Zero;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_MatrixAnimationGetValue))]
	private static void MatrixAnimationGetValue(IntPtr cPtr, ref Matrix src, ref Matrix dst, IntPtr clockType, IntPtr clockPtr, ref Matrix value)
	{
		try
		{
			MatrixAnimationBase matrixAnimationBase = (MatrixAnimationBase)GetExtendInstance(cPtr);
			AnimationClock animationClock = (AnimationClock)GetProxy(clockType, clockPtr, ownMemory: false);
			value = matrixAnimationBase.GetCurrentValueCore(src, dst, animationClock);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	internal static int GetNativePropertyType(Type type)
	{
		if (type.Equals(typeof(string)))
		{
			return 39;
		}
		if (type.Equals(typeof(bool)))
		{
			return 0;
		}
		if (type.Equals(typeof(float)))
		{
			return 1;
		}
		if (type.Equals(typeof(double)) || type.Equals(typeof(decimal)))
		{
			return 2;
		}
		if (type.Equals(typeof(int)) || type.Equals(typeof(long)))
		{
			return 5;
		}
		if (type.Equals(typeof(uint)) || type.Equals(typeof(ulong)) || type.Equals(typeof(char)))
		{
			return 6;
		}
		if (type.Equals(typeof(short)) || type.Equals(typeof(sbyte)))
		{
			return 7;
		}
		if (type.Equals(typeof(ushort)) || type.Equals(typeof(byte)))
		{
			return 8;
		}
		if (type.Equals(typeof(Color)))
		{
			return 9;
		}
		if (type.Equals(typeof(Point)))
		{
			return 10;
		}
		if (type.Equals(typeof(Rect)))
		{
			return 11;
		}
		if (type.Equals(typeof(Int32Rect)))
		{
			return 12;
		}
		if (type.Equals(typeof(Size)))
		{
			return 13;
		}
		if (type.Equals(typeof(Thickness)))
		{
			return 14;
		}
		if (type.Equals(typeof(CornerRadius)))
		{
			return 15;
		}
		if (type.Equals(typeof(TimeSpan)))
		{
			return 16;
		}
		if (type.Equals(typeof(Duration)))
		{
			return 17;
		}
		if (type.Equals(typeof(KeyTime)))
		{
			return 18;
		}
		if (type.Equals(typeof(bool?)))
		{
			return 19;
		}
		if (type.Equals(typeof(float?)))
		{
			return 20;
		}
		if (type.Equals(typeof(double?)) || type.Equals(typeof(decimal?)))
		{
			return 21;
		}
		if (type.Equals(typeof(int?)) || type.Equals(typeof(long?)))
		{
			return 24;
		}
		if (type.Equals(typeof(uint?)) || type.Equals(typeof(ulong?)) || type.Equals(typeof(char?)))
		{
			return 25;
		}
		if (type.Equals(typeof(short?)) || type.Equals(typeof(sbyte?)))
		{
			return 26;
		}
		if (type.Equals(typeof(ushort?)) || type.Equals(typeof(byte?)))
		{
			return 27;
		}
		if (type.Equals(typeof(Color?)))
		{
			return 28;
		}
		if (type.Equals(typeof(Point?)))
		{
			return 29;
		}
		if (type.Equals(typeof(Rect?)))
		{
			return 30;
		}
		if (type.Equals(typeof(Int32Rect?)))
		{
			return 31;
		}
		if (type.Equals(typeof(Size?)))
		{
			return 32;
		}
		if (type.Equals(typeof(Thickness?)))
		{
			return 33;
		}
		if (type.Equals(typeof(CornerRadius?)))
		{
			return 34;
		}
		if (type.Equals(typeof(TimeSpan?)))
		{
			return 35;
		}
		if (type.Equals(typeof(Duration?)))
		{
			return 36;
		}
		if (type.Equals(typeof(KeyTime?)))
		{
			return 37;
		}
		if (type.GetTypeInfo().IsEnum)
		{
			EnsureNativeType(type);
			return 38;
		}
		return 42;
	}

	private static PropertyAccessor GetProperty(IntPtr nativeType, int propertyIndex)
	{
		return ((NativeTypePropsInfo)GetNativeTypeInfo(nativeType)).Properties[propertyIndex];
	}

	private static T GetPropertyValue<T>(PropertyAccessor prop, object instance)
	{
		if (instance == null)
		{
			return default(T);
		}
		return ((PropertyAccessorT<T>)prop).Get(instance);
	}

	private static T GetPropertyValueNullable<T>(PropertyAccessor prop, object instance, out bool isNull) where T : struct
	{
		if (!prop.IsNullable)
		{
			isNull = false;
			return GetPropertyValue<T>(prop, instance);
		}
		T? propertyValue = GetPropertyValue<T?>(prop, instance);
		isNull = !propertyValue.HasValue;
		return propertyValue.GetValueOrDefault();
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Bool))]
	private static bool GetPropertyValue_Bool(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref bool isNull)
	{
		try
		{
			return GetPropertyValueNullable<bool>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return false;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Float))]
	private static float GetPropertyValue_Float(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref bool isNull)
	{
		try
		{
			return GetPropertyValueNullable<float>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0f;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Double))]
	private static double GetPropertyValue_Double(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref bool isNull)
	{
		try
		{
			return GetPropertyValueNullable<double>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0.0;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Int64))]
	private static long GetPropertyValue_Int64(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref bool isNull)
	{
		try
		{
			return GetPropertyValueNullable<long>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0L;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_UInt64))]
	private static ulong GetPropertyValue_UInt64(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref bool isNull)
	{
		try
		{
			return GetPropertyValueNullable<ulong>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0uL;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Int))]
	private static int GetPropertyValue_Int(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref bool isNull)
	{
		try
		{
			return GetPropertyValueNullable<int>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_UInt))]
	private static uint GetPropertyValue_UInt(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref bool isNull)
	{
		try
		{
			return GetPropertyValueNullable<uint>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0u;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Short))]
	private static short GetPropertyValue_Short(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref bool isNull)
	{
		try
		{
			return GetPropertyValueNullable<short>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_UShort))]
	private static ushort GetPropertyValue_UShort(IntPtr nativeType, int propertyIndex, IntPtr cPtr, [MarshalAs(UnmanagedType.U1)] ref bool isNull)
	{
		try
		{
			return GetPropertyValueNullable<ushort>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return 0;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_String))]
	private static IntPtr GetPropertyValue_String(IntPtr nativeType, int propertyIndex, IntPtr cPtr)
	{
		try
		{
			string propertyValue = GetPropertyValue<string>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr));
			return Marshal.StringToHGlobalUni((propertyValue != null) ? propertyValue : string.Empty);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return Marshal.StringToHGlobalUni(string.Empty);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Uri))]
	private static IntPtr GetPropertyValue_Uri(IntPtr nativeType, int propertyIndex, IntPtr cPtr)
	{
		try
		{
			Uri propertyValue = GetPropertyValue<Uri>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr));
			return Marshal.StringToHGlobalUni((propertyValue != null) ? propertyValue.OriginalString : string.Empty);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return Marshal.StringToHGlobalUni(string.Empty);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Color))]
	private static void GetPropertyValue_Color(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Color value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<Color>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Point))]
	private static void GetPropertyValue_Point(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Point value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<Point>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Rect))]
	private static void GetPropertyValue_Rect(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Rect value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<Rect>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Int32Rect))]
	private static void GetPropertyValue_Int32Rect(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Int32Rect value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<Int32Rect>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Size))]
	private static void GetPropertyValue_Size(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Size value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<Size>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Thickness))]
	private static void GetPropertyValue_Thickness(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Thickness value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<Thickness>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_CornerRadius))]
	private static void GetPropertyValue_CornerRadius(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref CornerRadius value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<CornerRadius>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_TimeSpan))]
	private static void GetPropertyValue_TimeSpan(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref TimeSpanStruct value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<TimeSpanStruct>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Duration))]
	private static void GetPropertyValue_Duration(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Duration value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<Duration>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_KeyTime))]
	private static void GetPropertyValue_KeyTime(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref KeyTime value, ref bool isNull)
	{
		try
		{
			value = GetPropertyValueNullable<KeyTime>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), out isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_Type))]
	private static IntPtr GetPropertyValue_Type(IntPtr nativeType, int propertyIndex, IntPtr cPtr)
	{
		try
		{
			Type propertyValue = GetPropertyValue<Type>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr));
			return (propertyValue != null) ? GetNativeType(propertyValue) : IntPtr.Zero;
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return IntPtr.Zero;
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GetPropertyValue_BaseComponent))]
	private static IntPtr GetPropertyValue_BaseComponent(IntPtr nativeType, int propertyIndex, IntPtr cPtr)
	{
		try
		{
			return GetInstanceHandle(GetPropertyValue<object>(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr))).Handle;
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
			return IntPtr.Zero;
		}
	}

	private static void SetPropertyValue<T>(PropertyAccessor prop, object instance, T value)
	{
		if (instance != null)
		{
			((PropertyAccessorT<T>)prop).Set(instance, value);
		}
	}

	private static void SetPropertyValueNullable<T>(PropertyAccessor prop, object instance, T value, bool isNull) where T : struct
	{
		if (!prop.IsNullable)
		{
			SetPropertyValue(prop, instance, value);
		}
		else
		{
			SetPropertyValue(prop, instance, isNull ? ((T?)null) : new T?(value));
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Bool))]
	private static void SetPropertyValue_Bool(IntPtr nativeType, int propertyIndex, IntPtr cPtr, bool val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Float))]
	private static void SetPropertyValue_Float(IntPtr nativeType, int propertyIndex, IntPtr cPtr, float val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Double))]
	private static void SetPropertyValue_Double(IntPtr nativeType, int propertyIndex, IntPtr cPtr, double val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Int64))]
	private static void SetPropertyValue_Int64(IntPtr nativeType, int propertyIndex, IntPtr cPtr, long val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_UInt64))]
	private static void SetPropertyValue_UInt64(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ulong val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Int))]
	private static void SetPropertyValue_Int(IntPtr nativeType, int propertyIndex, IntPtr cPtr, int val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_UInt))]
	private static void SetPropertyValue_UInt(IntPtr nativeType, int propertyIndex, IntPtr cPtr, uint val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Short))]
	private static void SetPropertyValue_Short(IntPtr nativeType, int propertyIndex, IntPtr cPtr, short val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_UShort))]
	private static void SetPropertyValue_UShort(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ushort val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_String))]
	private static void SetPropertyValue_String(IntPtr nativeType, int propertyIndex, IntPtr cPtr, IntPtr val)
	{
		try
		{
			SetPropertyValue(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), StringFromNativeUtf8(val));
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Uri))]
	private static void SetPropertyValue_Uri(IntPtr nativeType, int propertyIndex, IntPtr cPtr, IntPtr val)
	{
		try
		{
			Uri value = new Uri(StringFromNativeUtf8(val), UriKind.RelativeOrAbsolute);
			SetPropertyValue(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), value);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Color))]
	private static void SetPropertyValue_Color(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Color val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Point))]
	private static void SetPropertyValue_Point(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Point val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Rect))]
	private static void SetPropertyValue_Rect(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Rect val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Int32Rect))]
	private static void SetPropertyValue_Int32Rect(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Int32Rect val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Size))]
	private static void SetPropertyValue_Size(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Size val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Thickness))]
	private static void SetPropertyValue_Thickness(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Thickness val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_CornerRadius))]
	private static void SetPropertyValue_CornerRadius(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref CornerRadius val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_TimeSpan))]
	private static void SetPropertyValue_TimeSpan(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref TimeSpanStruct val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Duration))]
	private static void SetPropertyValue_Duration(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref Duration val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_KeyTime))]
	private static void SetPropertyValue_KeyTime(IntPtr nativeType, int propertyIndex, IntPtr cPtr, ref KeyTime val, bool isNull)
	{
		try
		{
			SetPropertyValueNullable(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), val, isNull);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_Type))]
	private static void SetPropertyValue_Type(IntPtr nativeType, int propertyIndex, IntPtr cPtr, IntPtr val)
	{
		try
		{
			NativeTypeInfo nativeTypeInfo = GetNativeTypeInfo(val);
			SetPropertyValue(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), nativeTypeInfo.Type);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_SetPropertyValue_BaseComponent))]
	private static void SetPropertyValue_BaseComponent(IntPtr nativeType, int propertyIndex, IntPtr cPtr, IntPtr valType, IntPtr val)
	{
		try
		{
			SetPropertyValue(GetProperty(nativeType, propertyIndex), GetExtendInstance(cPtr), GetProxy(valType, val, ownMemory: false));
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	public static bool NeedsCreateCPtr(Type extendType)
	{
		if (!(_cPtr == IntPtr.Zero))
		{
			return _extendType != extendType;
		}
		return true;
	}

	public static IntPtr GetCPtr(BaseComponent instance, Type extendType)
	{
		if (_cPtr == IntPtr.Zero)
		{
			throw new InvalidOperationException("cPtr is null");
		}
		if (_extendType != extendType)
		{
			throw new InvalidOperationException("Invalid extend type");
		}
		return _cPtr;
	}

	public static IntPtr NewCPtr(Type type)
	{
		IntPtr intPtr = Noesis_InstantiateExtend(EnsureNativeType(type));
		if (intPtr == IntPtr.Zero)
		{
			throw new Exception($"Unable to create an instance of '{type.FullName}'");
		}
		return intPtr;
	}

	[MonoPInvokeCallback(typeof(Callback_CreateInstance))]
	private static void CreateInstance(IntPtr nativeType, IntPtr cPtr)
	{
		try
		{
			NativeTypeExtendedInfo nativeTypeExtendedInfo = (NativeTypeExtendedInfo)GetNativeTypeInfo(nativeType);
			bool num = typeof(BaseComponent).GetTypeInfo().IsAssignableFrom(nativeTypeExtendedInfo.Type.GetTypeInfo());
			if (num)
			{
				_cPtr = cPtr;
				_extendType = nativeTypeExtendedInfo.Type;
			}
			if (nativeTypeExtendedInfo.Creator == null)
			{
				throw new InvalidOperationException($"Can't create an instance of {nativeTypeExtendedInfo.Type.FullName}, default constructor is not available");
			}
			object instance = nativeTypeExtendedInfo.Creator();
			if (num)
			{
				_cPtr = IntPtr.Zero;
				_extendType = null;
			}
			else
			{
				AddExtendInfo(cPtr, instance);
				BaseComponent.AddReference(cPtr);
				RegisterInterfaces(instance);
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_DeleteInstance))]
	private static void DeleteInstance(IntPtr cPtr)
	{
		try
		{
			RemoveExtendInfo(cPtr);
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	[MonoPInvokeCallback(typeof(Callback_GrabInstance))]
	private static void GrabInstance(IntPtr cPtr, bool grab)
	{
		try
		{
			if (!Initialized)
			{
				return;
			}
			ExtendInfo extendInfo = GetExtendInfo(cPtr);
			if (extendInfo != null)
			{
				if (grab)
				{
					extendInfo.instance = extendInfo.weak.Target;
				}
				else
				{
					extendInfo.instance = null;
				}
			}
		}
		catch (Exception exception)
		{
			Error.UnhandledException(exception);
		}
	}

	public static bool IsGrabbed(object o)
	{
		lock (_extends)
		{
			return _extends.Any((KeyValuePair<long, ExtendInfo> x) => x.Value.instance == o);
		}
	}

	public static void RegisterExtendInstance(BaseComponent instance)
	{
		AddExtendInfo(BaseComponent.getCPtr(instance).Handle, instance);
	}

	public static object GetExtendInstance(IntPtr cPtr)
	{
		return GetExtendInfo(cPtr)?.weak.Target;
	}

	public static object GetExtendInstance(IntPtr cPtr, bool ownMemory)
	{
		object result = null;
		ExtendInfo extendInfo = GetExtendInfo(cPtr);
		if (extendInfo != null)
		{
			result = extendInfo.weak.Target;
		}
		if (ownMemory)
		{
			BaseComponent.Release(cPtr);
		}
		return result;
	}

	private static ExtendInfo GetExtendInfo(IntPtr cPtr)
	{
		lock (_extends)
		{
			if (!_extends.TryGetValue(cPtr.ToInt64(), out var value))
			{
				if (Initialized)
				{
					Log.Error("Extend already removed");
				}
				return null;
			}
			if (value == null || value.weak.Target == null)
			{
				if (Initialized)
				{
					Log.Error("Extend already destroyed");
				}
				return null;
			}
			return value;
		}
	}

	private static void AddExtendInfo(IntPtr cPtr, object instance)
	{
		if (cPtr == IntPtr.Zero)
		{
			throw new InvalidOperationException("Native pointer of registered extend instance is null");
		}
		ExtendInfo value = new ExtendInfo
		{
			weak = new WeakReference(instance)
		};
		lock (_extends)
		{
			_extends.Add(cPtr.ToInt64(), value);
			if (!(instance is BaseComponent))
			{
				_weakExtends.Add(instance, new ExtendNotifier
				{
					cPtr = cPtr
				});
			}
		}
	}

	private static void RemoveExtendInfo(IntPtr cPtr)
	{
		lock (_extends)
		{
			_extends.Remove(cPtr.ToInt64());
		}
	}

	private static BaseComponent GetProxyInstance(IntPtr cPtr, bool ownMemory, NativeTypeInfo info)
	{
		if (cPtr == DependencyProperty.UnsetValuePtr)
		{
			return (BaseComponent)DependencyProperty.UnsetValue;
		}
		if (cPtr == Binding.DoNothingPtr)
		{
			return (BaseComponent)Binding.DoNothing;
		}
		lock (_proxies)
		{
			long key = cPtr.ToInt64();
			if (_proxies.TryGetValue(key, out var value) && value != null)
			{
				BaseComponent baseComponent = (BaseComponent)value.Target;
				if (!(baseComponent == null))
				{
					if (ownMemory)
					{
						BaseComponent.Release(cPtr);
					}
					return baseComponent;
				}
				_proxies.Remove(key);
			}
		}
		if (BaseComponent.GetNumReferences(cPtr) > 0)
		{
			return ((NativeTypeComponentInfo)info).Creator(cPtr, ownMemory);
		}
		return null;
	}

	public static BaseComponent AddProxy(BaseComponent instance)
	{
		long key = BaseComponent.getCPtr(instance).Handle.ToInt64();
		lock (_proxies)
		{
			if (_proxies.ContainsKey(key))
			{
				_proxies.Remove(key);
			}
			_proxies.Add(key, new WeakReference(instance));
			return instance;
		}
	}

	public static void RemoveProxy(IntPtr cPtr)
	{
		long key = cPtr.ToInt64();
		lock (_proxies)
		{
			if (_proxies.TryGetValue(key, out var value) && value != null && value.Target == null)
			{
				_proxies.Remove(key);
			}
		}
	}

	public static HandleRef GetInstanceHandle(object instance)
	{
		if (instance != null)
		{
			IntPtr intPtr = Box(instance);
			if (intPtr == IntPtr.Zero)
			{
				intPtr = FindInstancePtr(instance);
				if (intPtr == IntPtr.Zero && Initialized)
				{
					intPtr = NewCPtr(instance.GetType());
					AddExtendInfo(intPtr, instance);
					RegisterInterfaces(instance);
					if (instance is UnityEngine.Texture tex)
					{
						TextureSource.SetTexture(intPtr, tex);
					}
					else if (instance is Sprite sprite && sprite != null)
					{
						UnityEngine.Rect rect = ((sprite.packed && sprite.packingMode == SpritePackingMode.Rectangle) ? sprite.textureRect : sprite.rect);
						Int32Rect jarg = new Int32Rect((int)(rect.x * sprite.spriteAtlasTextureScale), sprite.texture.height - (int)((rect.height + rect.y) * sprite.spriteAtlasTextureScale), (int)(rect.width * sprite.spriteAtlasTextureScale), (int)(rect.height * sprite.spriteAtlasTextureScale));
						HandleRef jarg2 = new HandleRef(instance, intPtr);
						NoesisGUI_PINVOKE.CroppedBitmap_Source_set(jarg2, GetInstanceHandle(sprite.texture));
						NoesisGUI_PINVOKE.CroppedBitmap_SourceRect_set(jarg2, ref jarg);
					}
				}
			}
			return new HandleRef(instance, intPtr);
		}
		return new HandleRef(null, IntPtr.Zero);
	}

	private static IntPtr FindInstancePtr(object instance)
	{
		IntPtr zero = IntPtr.Zero;
		if (instance is BaseComponent)
		{
			return BaseComponent.getCPtr((BaseComponent)instance).Handle;
		}
		return FindWeakInstancePtr(instance);
	}

	private static IntPtr FindWeakInstancePtr(object instance)
	{
		IntPtr result = IntPtr.Zero;
		if (_weakExtends.TryGetValue(instance, out var value))
		{
			result = value.cPtr;
		}
		return result;
	}

	public static void AddPendingRelease(IntPtr cPtr)
	{
		lock (_pendingRelease)
		{
			_pendingRelease.Add(cPtr);
		}
	}

	private static void ReleasePending()
	{
		lock (_pendingRelease)
		{
			int count = _pendingRelease.Count;
			for (int i = 0; i < count; i++)
			{
				BaseComponent.Release(_pendingRelease[i]);
			}
			_pendingRelease.RemoveRange(0, count);
		}
	}

	public static void Update()
	{
		ReleasePending();
	}

	public static void RegisterInterfaces(object instance)
	{
		if (instance is INotifyPropertyChanged notifyPropertyChanged)
		{
			notifyPropertyChanged.PropertyChanged += NotifyPropertyChanged;
		}
		if (instance is INotifyCollectionChanged notifyCollectionChanged)
		{
			notifyCollectionChanged.CollectionChanged += NotifyCollectionChanged;
		}
		if (instance is ICommand command)
		{
			command.CanExecuteChanged += NotifyCanExecuteChanged;
		}
	}

	private static void NotifyPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (Initialized)
		{
			Noesis_LaunchPropertyChangedEvent(EnsureNativeType(sender.GetType()), GetInstanceHandle(sender).Handle, (e.PropertyName != null) ? e.PropertyName : string.Empty);
		}
	}

	private static void NotifyCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (Initialized)
		{
			IntPtr nativeType = EnsureNativeType(sender.GetType());
			object instance = ((e.NewItems != null && e.NewItems.Count > 0) ? e.NewItems[0] : null);
			Noesis_LaunchCollectionChangedEvent(oldItem: GetInstanceHandle((e.OldItems != null && e.OldItems.Count > 0) ? e.OldItems[0] : null).Handle, nativeType: nativeType, cPtr: GetInstanceHandle(sender).Handle, action: (int)e.Action, newItem: GetInstanceHandle(instance).Handle, newIndex: e.NewStartingIndex, oldIndex: e.OldStartingIndex);
		}
	}

	private static void NotifyCanExecuteChanged(object sender, System.EventArgs e)
	{
		if (Initialized)
		{
			Noesis_LaunchCanExecuteChangedEvent(EnsureNativeType(sender.GetType()), GetInstanceHandle(sender).Handle);
		}
	}

	private static Dictionary<Type, BoxDelegate> CreateBoxFunctions()
	{
		return new Dictionary<Type, BoxDelegate>(100)
		{
			[typeof(string)] = (object val) => NoesisGUI_.Box_String((string)val),
			[typeof(bool)] = (object val) => NoesisGUI_.Box_Bool((bool)val),
			[typeof(float)] = (object val) => NoesisGUI_.Box_Float((float)val),
			[typeof(double)] = (object val) => NoesisGUI_.Box_Double((double)val),
			[typeof(decimal)] = (object val) => NoesisGUI_.Box_Double((double)(decimal)val),
			[typeof(int)] = (object val) => NoesisGUI_.Box_Int((int)val),
			[typeof(uint)] = (object val) => NoesisGUI_.Box_UInt((uint)val),
			[typeof(long)] = (object val) => NoesisGUI_.Box_Long((long)val),
			[typeof(ulong)] = (object val) => NoesisGUI_.Box_ULong((ulong)val),
			[typeof(char)] = (object val) => NoesisGUI_.Box_UInt((char)val),
			[typeof(short)] = (object val) => NoesisGUI_.Box_Short((short)val),
			[typeof(sbyte)] = (object val) => NoesisGUI_.Box_Short((sbyte)val),
			[typeof(ushort)] = (object val) => NoesisGUI_.Box_UShort((ushort)val),
			[typeof(byte)] = (object val) => NoesisGUI_.Box_UShort((byte)val),
			[typeof(Color)] = (object val) => NoesisGUI_.Box_Color((Color)val),
			[typeof(Point)] = (object val) => NoesisGUI_.Box_Point((Point)val),
			[typeof(Rect)] = (object val) => NoesisGUI_.Box_Rect((Rect)val),
			[typeof(Int32Rect)] = (object val) => NoesisGUI_.Box_Int32Rect((Int32Rect)val),
			[typeof(Size)] = (object val) => NoesisGUI_.Box_Size((Size)val),
			[typeof(Thickness)] = (object val) => NoesisGUI_.Box_Thickness((Thickness)val),
			[typeof(CornerRadius)] = (object val) => NoesisGUI_.Box_CornerRadius((CornerRadius)val),
			[typeof(GridLength)] = (object val) => NoesisGUI_.Box_GridLength((GridLength)val),
			[typeof(Duration)] = (object val) => NoesisGUI_.Box_Duration((Duration)val),
			[typeof(KeyTime)] = (object val) => NoesisGUI_.Box_KeyTime((KeyTime)val),
			[typeof(TimeSpan)] = (object val) => NoesisGUI_.Box_TimeSpan((TimeSpan)val),
			[typeof(VirtualizationCacheLength)] = (object val) => NoesisGUI_.Box_VirtualizationCacheLength((VirtualizationCacheLength)val),
			[typeof(Matrix)] = (object val) => NoesisGUI_.Box_Matrix((Matrix)val),
			[typeof(Matrix3D)] = (object val) => NoesisGUI_.Box_Matrix3D((Matrix3D)val),
			[typeof(Matrix4)] = (object val) => NoesisGUI_.Box_Matrix4((Matrix4)val),
			[typeof(AlignmentX)] = (object val) => NoesisGUI_.Box_AlignmentX((AlignmentX)val),
			[typeof(AlignmentY)] = (object val) => NoesisGUI_.Box_AlignmentY((AlignmentY)val),
			[typeof(AutoToolTipPlacement)] = (object val) => NoesisGUI_.Box_AutoToolTipPlacement((AutoToolTipPlacement)val),
			[typeof(BindingMode)] = (object val) => NoesisGUI_.Box_BindingMode((BindingMode)val),
			[typeof(BitmapScalingMode)] = (object val) => NoesisGUI_.Box_BitmapScalingMode((BitmapScalingMode)val),
			[typeof(BrushMappingMode)] = (object val) => NoesisGUI_.Box_BrushMappingMode((BrushMappingMode)val),
			[typeof(CharacterCasing)] = (object val) => NoesisGUI_.Box_CharacterCasing((CharacterCasing)val),
			[typeof(ClickMode)] = (object val) => NoesisGUI_.Box_ClickMode((ClickMode)val),
			[typeof(ColorInterpolationMode)] = (object val) => NoesisGUI_.Box_ColorInterpolationMode((ColorInterpolationMode)val),
			[typeof(Dock)] = (object val) => NoesisGUI_.Box_Dock((Dock)val),
			[typeof(ExpandDirection)] = (object val) => NoesisGUI_.Box_ExpandDirection((ExpandDirection)val),
			[typeof(FillRule)] = (object val) => NoesisGUI_.Box_FillRule((FillRule)val),
			[typeof(FlowDirection)] = (object val) => NoesisGUI_.Box_FlowDirection((FlowDirection)val),
			[typeof(FontStretch)] = (object val) => NoesisGUI_.Box_FontStretch((FontStretch)val),
			[typeof(FontStyle)] = (object val) => NoesisGUI_.Box_FontStyle((FontStyle)val),
			[typeof(FontWeight)] = (object val) => NoesisGUI_.Box_FontWeight((FontWeight)val),
			[typeof(GeometryCombineMode)] = (object val) => NoesisGUI_.Box_GeometryCombineMode((GeometryCombineMode)val),
			[typeof(GradientSpreadMethod)] = (object val) => NoesisGUI_.Box_GradientSpreadMethod((GradientSpreadMethod)val),
			[typeof(HorizontalAlignment)] = (object val) => NoesisGUI_.Box_HorizontalAlignment((HorizontalAlignment)val),
			[typeof(MouseAction)] = (object val) => NoesisGUI_.Box_MouseAction((MouseAction)val),
			[typeof(Key)] = (object val) => NoesisGUI_.Box_Key((Key)val),
			[typeof(ModifierKeys)] = (object val) => NoesisGUI_.Box_ModifierKeys((ModifierKeys)val),
			[typeof(KeyboardNavigationMode)] = (object val) => NoesisGUI_.Box_KeyboardNavigationMode((KeyboardNavigationMode)val),
			[typeof(LineStackingStrategy)] = (object val) => NoesisGUI_.Box_LineStackingStrategy((LineStackingStrategy)val),
			[typeof(ListSortDirection)] = (object val) => NoesisGUI_.Box_ListSortDirection((ListSortDirection)val),
			[typeof(MenuItemRole)] = (object val) => NoesisGUI_.Box_MenuItemRole((MenuItemRole)val),
			[typeof(Orientation)] = (object val) => NoesisGUI_.Box_Orientation((Orientation)val),
			[typeof(OverflowMode)] = (object val) => NoesisGUI_.Box_OverflowMode((OverflowMode)val),
			[typeof(PenLineCap)] = (object val) => NoesisGUI_.Box_PenLineCap((PenLineCap)val),
			[typeof(PenLineJoin)] = (object val) => NoesisGUI_.Box_PenLineJoin((PenLineJoin)val),
			[typeof(PlacementMode)] = (object val) => NoesisGUI_.Box_PlacementMode((PlacementMode)val),
			[typeof(PopupAnimation)] = (object val) => NoesisGUI_.Box_PopupAnimation((PopupAnimation)val),
			[typeof(RelativeSourceMode)] = (object val) => NoesisGUI_.Box_RelativeSourceMode((RelativeSourceMode)val),
			[typeof(SelectionMode)] = (object val) => NoesisGUI_.Box_SelectionMode((SelectionMode)val),
			[typeof(ScrollBarVisibility)] = (object val) => NoesisGUI_.Box_ScrollBarVisibility((ScrollBarVisibility)val),
			[typeof(Stretch)] = (object val) => NoesisGUI_.Box_Stretch((Stretch)val),
			[typeof(StretchDirection)] = (object val) => NoesisGUI_.Box_StretchDirection((StretchDirection)val),
			[typeof(TextAlignment)] = (object val) => NoesisGUI_.Box_TextAlignment((TextAlignment)val),
			[typeof(TextDecorations)] = (object val) => NoesisGUI_.Box_TextDecorations((TextDecorations)val),
			[typeof(TextTrimming)] = (object val) => NoesisGUI_.Box_TextTrimming((TextTrimming)val),
			[typeof(TextWrapping)] = (object val) => NoesisGUI_.Box_TextWrapping((TextWrapping)val),
			[typeof(TickBarPlacement)] = (object val) => NoesisGUI_.Box_TickBarPlacement((TickBarPlacement)val),
			[typeof(TickPlacement)] = (object val) => NoesisGUI_.Box_TickPlacement((TickPlacement)val),
			[typeof(TileMode)] = (object val) => NoesisGUI_.Box_TileMode((TileMode)val),
			[typeof(VerticalAlignment)] = (object val) => NoesisGUI_.Box_VerticalAlignment((VerticalAlignment)val),
			[typeof(Visibility)] = (object val) => NoesisGUI_.Box_Visibility((Visibility)val),
			[typeof(ClockState)] = (object val) => NoesisGUI_.Box_ClockState((ClockState)val),
			[typeof(EasingMode)] = (object val) => NoesisGUI_.Box_EasingMode((EasingMode)val),
			[typeof(SlipBehavior)] = (object val) => NoesisGUI_.Box_SlipBehavior((SlipBehavior)val),
			[typeof(FillBehavior)] = (object val) => NoesisGUI_.Box_FillBehavior((FillBehavior)val),
			[typeof(GridViewColumnHeaderRole)] = (object val) => NoesisGUI_.Box_GridViewColumnHeaderRole((GridViewColumnHeaderRole)val),
			[typeof(HandoffBehavior)] = (object val) => NoesisGUI_.Box_HandoffBehavior((HandoffBehavior)val),
			[typeof(PanningMode)] = (object val) => NoesisGUI_.Box_PanningMode((PanningMode)val),
			[typeof(UpdateSourceTrigger)] = (object val) => NoesisGUI_.Box_UpdateSourceTrigger((UpdateSourceTrigger)val),
			[typeof(ScrollUnit)] = (object val) => NoesisGUI_.Box_ScrollUnit((ScrollUnit)val),
			[typeof(VirtualizationMode)] = (object val) => NoesisGUI_.Box_VirtualizationMode((VirtualizationMode)val),
			[typeof(VirtualizationCacheLengthUnit)] = (object val) => NoesisGUI_.Box_VirtualizationCacheLengthUnit((VirtualizationCacheLengthUnit)val),
			[typeof(PPAAMode)] = (object val) => NoesisGUI_.Box_PPAAMode((PPAAMode)val)
		};
	}

	public static IntPtr Box(object val)
	{
		if (_boxFunctions.TryGetValue(val.GetType(), out var value))
		{
			return RegisterPendingRelease(value(val));
		}
		if (val.GetType().GetTypeInfo().IsEnum)
		{
			IntPtr type = EnsureNativeType(val.GetType());
			ulong val2 = Convert.ToUInt64(val);
			return RegisterPendingRelease(NoesisGUI_.Box_CustomEnum(type, val2));
		}
		if (val is Type)
		{
			return RegisterPendingRelease(NoesisGUI_.Box_Type((Type)val));
		}
		return IntPtr.Zero;
	}

	private static IntPtr RegisterPendingRelease(IntPtr cPtr)
	{
		AddPendingRelease(cPtr);
		return cPtr;
	}

	private static Dictionary<Type, UnboxDelegate> CreateUnboxFunctions()
	{
		return new Dictionary<Type, UnboxDelegate>(100)
		{
			[typeof(Boxed<Type>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Type(cPtr),
			[typeof(Boxed<string>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_String(cPtr),
			[typeof(Boxed<bool>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Bool(cPtr),
			[typeof(Boxed<float>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Float(cPtr),
			[typeof(Boxed<double>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Double(cPtr),
			[typeof(Boxed<int>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Int(cPtr),
			[typeof(Boxed<uint>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_UInt(cPtr),
			[typeof(Boxed<long>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Long(cPtr),
			[typeof(Boxed<ulong>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_ULong(cPtr),
			[typeof(Boxed<short>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Short(cPtr),
			[typeof(Boxed<ushort>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_UShort(cPtr),
			[typeof(Boxed<Color>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Color(cPtr),
			[typeof(Boxed<Point>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Point(cPtr),
			[typeof(Boxed<Rect>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Rect(cPtr),
			[typeof(Boxed<Int32Rect>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Int32Rect(cPtr),
			[typeof(Boxed<Size>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Size(cPtr),
			[typeof(Boxed<Thickness>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Thickness(cPtr),
			[typeof(Boxed<CornerRadius>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_CornerRadius(cPtr),
			[typeof(Boxed<GridLength>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_GridLength(cPtr),
			[typeof(Boxed<Duration>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Duration(cPtr),
			[typeof(Boxed<KeyTime>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_KeyTime(cPtr),
			[typeof(Boxed<TimeSpan>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_TimeSpan(cPtr),
			[typeof(Boxed<VirtualizationCacheLength>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_VirtualizationCacheLength(cPtr),
			[typeof(Boxed<Matrix>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Matrix(cPtr),
			[typeof(Boxed<Matrix3D>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Matrix3D(cPtr),
			[typeof(Boxed<Matrix4>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Matrix4(cPtr),
			[typeof(Boxed<AlignmentX>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_AlignmentX(cPtr),
			[typeof(Boxed<AlignmentY>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_AlignmentY(cPtr),
			[typeof(Boxed<AutoToolTipPlacement>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_AutoToolTipPlacement(cPtr),
			[typeof(Boxed<BindingMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_BindingMode(cPtr),
			[typeof(Boxed<BitmapScalingMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_BitmapScalingMode(cPtr),
			[typeof(Boxed<BrushMappingMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_BrushMappingMode(cPtr),
			[typeof(Boxed<CharacterCasing>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_CharacterCasing(cPtr),
			[typeof(Boxed<ClickMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_ClickMode(cPtr),
			[typeof(Boxed<ColorInterpolationMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_ColorInterpolationMode(cPtr),
			[typeof(Boxed<Dock>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Dock(cPtr),
			[typeof(Boxed<ExpandDirection>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_ExpandDirection(cPtr),
			[typeof(Boxed<FillRule>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_FillRule(cPtr),
			[typeof(Boxed<FlowDirection>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_FlowDirection(cPtr),
			[typeof(Boxed<FontStretch>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_FontStretch(cPtr),
			[typeof(Boxed<FontStyle>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_FontStyle(cPtr),
			[typeof(Boxed<FontWeight>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_FontWeight(cPtr),
			[typeof(Boxed<GeometryCombineMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_GeometryCombineMode(cPtr),
			[typeof(Boxed<GradientSpreadMethod>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_GradientSpreadMethod(cPtr),
			[typeof(Boxed<HorizontalAlignment>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_HorizontalAlignment(cPtr),
			[typeof(Boxed<MouseAction>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_MouseAction(cPtr),
			[typeof(Boxed<Key>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Key(cPtr),
			[typeof(Boxed<ModifierKeys>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_ModifierKeys(cPtr),
			[typeof(Boxed<KeyboardNavigationMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_KeyboardNavigationMode(cPtr),
			[typeof(Boxed<LineStackingStrategy>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_LineStackingStrategy(cPtr),
			[typeof(Boxed<ListSortDirection>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_ListSortDirection(cPtr),
			[typeof(Boxed<MenuItemRole>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_MenuItemRole(cPtr),
			[typeof(Boxed<Orientation>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Orientation(cPtr),
			[typeof(Boxed<OverflowMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_OverflowMode(cPtr),
			[typeof(Boxed<PenLineCap>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_PenLineCap(cPtr),
			[typeof(Boxed<PenLineJoin>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_PenLineJoin(cPtr),
			[typeof(Boxed<PlacementMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_PlacementMode(cPtr),
			[typeof(Boxed<PopupAnimation>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_PopupAnimation(cPtr),
			[typeof(Boxed<RelativeSourceMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_RelativeSourceMode(cPtr),
			[typeof(Boxed<SelectionMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_SelectionMode(cPtr),
			[typeof(Boxed<ScrollBarVisibility>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_ScrollBarVisibility(cPtr),
			[typeof(Boxed<Stretch>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Stretch(cPtr),
			[typeof(Boxed<StretchDirection>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_StretchDirection(cPtr),
			[typeof(Boxed<TextAlignment>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_TextAlignment(cPtr),
			[typeof(Boxed<TextDecorations>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_TextDecorations(cPtr),
			[typeof(Boxed<TextTrimming>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_TextTrimming(cPtr),
			[typeof(Boxed<TextWrapping>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_TextWrapping(cPtr),
			[typeof(Boxed<TickBarPlacement>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_TickBarPlacement(cPtr),
			[typeof(Boxed<TickPlacement>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_TickPlacement(cPtr),
			[typeof(Boxed<TileMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_TileMode(cPtr),
			[typeof(Boxed<VerticalAlignment>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_VerticalAlignment(cPtr),
			[typeof(Boxed<Visibility>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_Visibility(cPtr),
			[typeof(Boxed<ClockState>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_ClockState(cPtr),
			[typeof(Boxed<EasingMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_EasingMode(cPtr),
			[typeof(Boxed<SlipBehavior>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_SlipBehavior(cPtr),
			[typeof(Boxed<FillBehavior>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_FillBehavior(cPtr),
			[typeof(Boxed<GridViewColumnHeaderRole>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_GridViewColumnHeaderRole(cPtr),
			[typeof(Boxed<HandoffBehavior>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_HandoffBehavior(cPtr),
			[typeof(Boxed<PanningMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_PanningMode(cPtr),
			[typeof(Boxed<UpdateSourceTrigger>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_UpdateSourceTrigger(cPtr),
			[typeof(Boxed<ScrollUnit>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_ScrollUnit(cPtr),
			[typeof(Boxed<VirtualizationMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_VirtualizationMode(cPtr),
			[typeof(Boxed<VirtualizationCacheLengthUnit>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_VirtualizationCacheLengthUnit(cPtr),
			[typeof(Boxed<PPAAMode>)] = (IntPtr cPtr) => NoesisGUI_.Unbox_PPAAMode(cPtr)
		};
	}

	public static object Unbox(IntPtr cPtr, bool ownMemory, NativeTypeInfo info)
	{
		object result = null;
		if (_unboxFunctions.TryGetValue(info.Type, out var value))
		{
			result = value(cPtr);
		}
		else if (info.Type == typeof(Boxed<Enum>))
		{
			ulong enumVal = 0uL;
			result = Enum.ToObject(GetNativeTypeInfo(NoesisGUI_.Unbox_CustomEnum(cPtr, ref enumVal)).Type, enumVal);
		}
		else
		{
			Log.Error("Can't unbox native pointer");
		}
		if (ownMemory)
		{
			BaseComponent.Release(cPtr);
		}
		return result;
	}

	[DllImport("Noesis")]
	private static extern int Noesis_GetNativeTypes(IntPtr[] types, int capacity);

	[DllImport("Noesis")]
	private static extern IntPtr Noesis_GetTypeName(IntPtr type);

	[DllImport("Noesis")]
	private static extern IntPtr Noesis_RegisterEnumType([MarshalAs(UnmanagedType.LPStr)] string typeName, int numEnums, IntPtr enumsData);

	[DllImport("Noesis")]
	private static extern void Noesis_FillExtendType(ref ExtendTypeData typeData, int numProps, IntPtr propsData);

	[DllImport("Noesis")]
	private static extern IntPtr Noesis_InstantiateExtend(IntPtr nativeType);

	[DllImport("Noesis")]
	private static extern void Noesis_LaunchPropertyChangedEvent(IntPtr nativeType, IntPtr cPtr, [MarshalAs(UnmanagedType.LPStr)] string propertyName);

	[DllImport("Noesis")]
	private static extern void Noesis_LaunchCollectionChangedEvent(IntPtr nativeType, IntPtr cPtr, int action, IntPtr newItem, IntPtr oldItem, int newIndex, int oldIndex);

	[DllImport("Noesis")]
	private static extern void Noesis_LaunchCanExecuteChangedEvent(IntPtr nativeType, IntPtr cPtr);

	[DllImport("Noesis")]
	private static extern void Noesis_EnableExtend(bool enable);

	[DllImport("Noesis")]
	private static extern void Noesis_ClearExtendTypes();

	[DllImport("Noesis")]
	private static extern void Noesis_RegisterReflectionCallbacks(Callback_FreeString callback_FreeString, Callback_RegisterType callback_RegisterType, Callback_ToString callback_ToString, Callback_Equals callback_Equals, Callback_VisualChildrenCount callback_VisualChildrenCount, Callback_VisualGetChild callback_VisualGetChild, Callback_UIElementRender callback_UIElementRender, Callback_FrameworkElementConnectEvent callback_FrameworkElementConnectEvent, Callback_FrameworkElementConnectField callback_FrameworkElementConnectField, Callback_FrameworkElementMeasure callback_FrameworkElementMeasure, Callback_FrameworkElementArrange callback_FrameworkElementArrange, Callback_FrameworkElementApplyTemplate callback_FrameworkElementApplyTemplate, Callback_ItemsControlGetContainer callback_ItemsControlGetContainer, Callback_ItemsControlIsContainer callback_ItemsControlIsContainer, Callback_AdornerGetTransform callback_AdornerGetTransform, Callback_FreezableClone callback_FreezableClone, Callback_ShapeGetGeometry callback_ShapeGetGeometry, Callback_CommandCanExecute callback_CommandCanExecute, Callback_CommandExecute callback_CommandExecute, Callback_ConverterConvert callback_ConverterConvert, Callback_ConverterConvertBack callback_ConverterConvertBack, Callback_MultiConverterConvert callback_MultiConverterConvert, Callback_MultiConverterConvertBack callback_MultiConverterConvertBack, Callback_ListCount callback_ListCount, Callback_ListGet callback_ListGet, Callback_ListSet callback_ListSet, Callback_ListAdd callback_ListAdd, Callback_ListInsert callback_ListInsert, Callback_ListIndexOf callback_ListIndexOf, Callback_ListRemoveAt callback_ListRemoveAt, Callback_ListClear callback_ListClear, Callback_DictionaryFind callback_DictionaryFind, Callback_DictionarySet callback_DictionarySet, Callback_DictionaryAdd callback_DictionaryAdd, Callback_DictionaryRemove callback_DictionaryRemove, Callback_ListIndexerTryGet callback_ListIndexerTryGet, Callback_ListIndexerTrySet callback_ListIndexerTrySet, Callback_DictionaryIndexerTryGet callback_DictionaryIndexerTryGet, Callback_DictionaryIndexerTrySet callback_DictionaryIndexerTrySet, Callback_SelectTemplate callback_SelectTemplate, Callback_StreamSetPosition callback_StreamSetPosition, Callback_StreamGetPosition callback_StreamGetPosition, Callback_StreamGetLength callback_StreamGetLength, Callback_StreamRead callback_StreamRead, Callback_StreamClose callback_StreamClose, Callback_ProviderLoadXaml callback_ProviderLoadXaml, Callback_ProviderTextureInfo callback_ProviderTextureInfo, Callback_ProviderTextureLoad callback_ProviderTextureLoad, Callback_ProviderTextureOpen callback_ProviderTextureOpen, Callback_ProviderMatchFont callback_ProviderMatchFont, Callback_ProviderFamilyExists callback_ProviderFamilyExists, Callback_ProviderScanFolder callback_ProviderScanFolder, Callback_ProviderOpenFont callback_ProviderOpenFont, Callback_ScrollInfoBringIntoView callback_ScrollInfoBringIntoView, Callback_ScrollInfoGetCanHorizontalScroll callback_ScrollInfoGetCanHorizontalScroll, Callback_ScrollInfoSetCanHorizontalScroll callback_ScrollInfoSetCanHorizontalScroll, Callback_ScrollInfoGetCanVerticalScroll callback_ScrollInfoGetCanVerticalScroll, Callback_ScrollInfoSetCanVerticalScroll callback_ScrollInfoSetCanVerticalScroll, Callback_ScrollInfoGetExtentWidth callback_ScrollInfoGetExtentWidth, Callback_ScrollInfoGetExtentHeight callback_ScrollInfoGetExtentHeigth, Callback_ScrollInfoGetViewportWidth callback_ScrollInfoGetViewportWidth, Callback_ScrollInfoGetViewportHeight callback_ScrollInfoGetViewportHeight, Callback_ScrollInfoGetHorizontalOffset callback_ScrollInfoGetHorizontalOffset, Callback_ScrollInfoGetVerticalOffset callback_ScrollInfoGetVerticalOffset, Callback_ScrollInfoGetScrollOwner callback_ScrollInfoGetScrollOwner, Callback_ScrollInfoSetScrollOwner callback_ScrollInfoSetScrollOwner, Callback_ScrollInfoLineLeft callback_ScrollInfoLineLeft, Callback_ScrollInfoLineRight callback_ScrollInfoLineRight, Callback_ScrollInfoLineUp callback_ScrollInfoLineUp, Callback_ScrollInfoLineDown callback_ScrollInfoLineDown, Callback_ScrollInfoPageLeft callback_ScrollInfoPageLeft, Callback_ScrollInfoPageRight callback_ScrollInfoPageRight, Callback_ScrollInfoPageUp callback_ScrollInfoPageUp, Callback_ScrollInfoPageDown callback_ScrollInfoPageDown, Callback_ScrollInfoMouseWheelLeft callback_ScrollInfoMouseWheelLeft, Callback_ScrollInfoMouseWheelRight callback_ScrollInfoMouseWheelRight, Callback_ScrollInfoMouseWheelUp callback_ScrollInfoMouseWheelUp, Callback_ScrollInfoMouseWheelDown callback_ScrollInfoMouseWheelDown, Callback_ScrollInfoSetHorizontalOffset callback_ScrollInfoSetHorizontalOffset, Callback_ScrollInfoSetVerticalOffset callback_ScrollInfoSetVerticalOffset, Callback_ScrollInfoMakeVisible callback_ScrollInfoMakeVisible, Callback_MarkupExtensionProvideValue callback_MarkupExtensionProvideValue, Callback_AnimationTimelineGetType callback_AnimationTimelineGetType, Callback_AnimationTimelineGetValue callback_AnimationTimelineGetValue, Callback_BooleanAnimationGetValue callback_BooleanAnimationGetValue, Callback_Int16AnimationGetValue callback_Int16AnimationGetValue, Callback_Int32AnimationGetValue callback_Int32AnimationGetValue, Callback_Int64AnimationGetValue callback_Int64AnimationGetValue, Callback_DoubleAnimationGetValue callback_DoubleAnimationGetValue, Callback_ColorAnimationGetValue callback_ColorAnimationGetValue, Callback_PointAnimationGetValue callback_PointAnimationGetValue, Callback_RectAnimationGetValue callback_RectAnimationGetValue, Callback_SizeAnimationGetValue callback_SizeAnimationGetValue, Callback_ThicknessAnimationGetValue callback_ThicknessAnimationGetValue, Callback_ObjectAnimationGetValue callback_ObjectAnimationGetValue, Callback_StringAnimationGetValue callback_StringAnimationGetValue, Callback_MatrixAnimationGetValue callback_MatrixAnimationGetValue, Callback_GetPropertyValue_Bool callback_GetPropertyValue_Bool, Callback_GetPropertyValue_Float callback_GetPropertyValue_Float, Callback_GetPropertyValue_Double callback_GetPropertyValue_Double, Callback_GetPropertyValue_Int64 callback_GetPropertyValue_Int64, Callback_GetPropertyValue_UInt64 callback_GetPropertyValue_UInt64, Callback_GetPropertyValue_Int callback_GetPropertyValue_Int, Callback_GetPropertyValue_UInt callback_GetPropertyValue_UInt, Callback_GetPropertyValue_Short callback_GetPropertyValue_Short, Callback_GetPropertyValue_UShort callback_GetPropertyValue_UShort, Callback_GetPropertyValue_String callback_GetPropertyValue_String, Callback_GetPropertyValue_Uri callback_GetPropertyValue_Uri, Callback_GetPropertyValue_Color callback_GetPropertyValue_Color, Callback_GetPropertyValue_Point callback_GetPropertyValue_Point, Callback_GetPropertyValue_Rect callback_GetPropertyValue_Rect, Callback_GetPropertyValue_Int32Rect callback_GetPropertyValue_Int32Rect, Callback_GetPropertyValue_Size callback_GetPropertyValue_Size, Callback_GetPropertyValue_Thickness callback_GetPropertyValue_Thickness, Callback_GetPropertyValue_CornerRadius callback_GetPropertyValue_CornerRadius, Callback_GetPropertyValue_TimeSpan callback_GetPropertyValue_TimeSpan, Callback_GetPropertyValue_Duration callback_GetPropertyValue_Duration, Callback_GetPropertyValue_KeyTime callback_GetPropertyValue_KeyTime, Callback_GetPropertyValue_Type callback_GetPropertyValue_Type, Callback_GetPropertyValue_BaseComponent callback_GetPropertyValue_BaseComponent, Callback_SetPropertyValue_Bool callback_SetPropertyValue_Bool, Callback_SetPropertyValue_Float callback_SetPropertyValue_Float, Callback_SetPropertyValue_Double callback_SetPropertyValue_Double, Callback_SetPropertyValue_Int64 callback_SetPropertyValue_Int64, Callback_SetPropertyValue_UInt64 callback_SetPropertyValue_UInt64, Callback_SetPropertyValue_Int callback_SetPropertyValue_Int, Callback_SetPropertyValue_UInt callback_SetPropertyValue_UInt, Callback_SetPropertyValue_Short callback_SetPropertyValue_Short, Callback_SetPropertyValue_UShort callback_SetPropertyValue_UShort, Callback_SetPropertyValue_String callback_SetPropertyValue_String, Callback_SetPropertyValue_Uri callback_SetPropertyValue_Uri, Callback_SetPropertyValue_Color callback_SetPropertyValue_Color, Callback_SetPropertyValue_Point callback_SetPropertyValue_Point, Callback_SetPropertyValue_Rect callback_SetPropertyValue_Rect, Callback_SetPropertyValue_Int32Rect callback_SetPropertyValue_Int32Rect, Callback_SetPropertyValue_Size callback_SetPropertyValue_Size, Callback_SetPropertyValue_Thickness callback_SetPropertyValue_Thickness, Callback_SetPropertyValue_CornerRadius callback_SetPropertyValue_CornerRadius, Callback_SetPropertyValue_TimeSpan callback_SetPropertyValue_TimeSpan, Callback_SetPropertyValue_Duration callback_SetPropertyValue_Duration, Callback_SetPropertyValue_KeyTime callback_SetPropertyValue_KeyTime, Callback_SetPropertyValue_Type callback_SetPropertyValue_Type, Callback_SetPropertyValue_BaseComponent callback_SetPropertyValue_BaseComponent, Callback_CreateInstance callback_CreateInstance, Callback_DeleteInstance callback_DeleteInstance, Callback_GrabInstance callback_GrabInstance);

	private static Func<object, ReturnT> CreateGetter<ReturnT>(Type type, MethodInfo method)
	{
		MethodInfo methodInfo = CreateGetterHelperMethod.MakeGenericMethod(type, method.ReturnType);
		object[] parameters = new object[1] { method };
		return (Func<object, ReturnT>)methodInfo.Invoke(null, parameters);
	}

	private static Func<object, ReturnT> CreateGetterHelper<InstanceT, ReturnT>(MethodInfo method)
	{
		Func<InstanceT, ReturnT> func = (Func<InstanceT, ReturnT>)method.CreateDelegate(typeof(Func<InstanceT, ReturnT>));
		return (object target) => func((InstanceT)target);
	}

	private static Action<object, ValueT> CreateSetter<ValueT>(Type type, MethodInfo method)
	{
		MethodInfo methodInfo = CreateSetterHelperMethod.MakeGenericMethod(type, method.GetParameters()[0].ParameterType);
		object[] parameters = new object[1] { method };
		return (Action<object, ValueT>)methodInfo.Invoke(null, parameters);
	}

	private static Action<object, ValueT> CreateSetterHelper<InstanceT, ValueT>(MethodInfo method)
	{
		Action<InstanceT, ValueT> func = (Action<InstanceT, ValueT>)method.CreateDelegate(typeof(Action<InstanceT, ValueT>));
		return delegate(object target, ValueT value)
		{
			func((InstanceT)target, value);
		};
	}

	private static Func<object, IndexT, object> CreateIndexerGetter<IndexT>(Type type, MethodInfo method)
	{
		MethodInfo methodInfo = CreateIndexerGetterHelperMethod.MakeGenericMethod(type, method.GetParameters()[0].ParameterType, method.ReturnType);
		object[] parameters = new object[1] { method };
		return (Func<object, IndexT, object>)methodInfo.Invoke(null, parameters);
	}

	private static Func<object, IndexT, object> CreateIndexerGetterHelper<InstanceT, IndexT, ReturnT>(MethodInfo method)
	{
		Func<InstanceT, IndexT, ReturnT> func = (Func<InstanceT, IndexT, ReturnT>)method.CreateDelegate(typeof(Func<InstanceT, IndexT, ReturnT>));
		return (object target, IndexT index) => func((InstanceT)target, index);
	}

	private static Action<object, IndexT, object> CreateIndexerSetter<IndexT>(Type type, MethodInfo method)
	{
		ParameterInfo[] parameters = method.GetParameters();
		MethodInfo methodInfo = CreateIndexerSetterHelperMethod.MakeGenericMethod(type, parameters[0].ParameterType, parameters[1].ParameterType);
		object[] parameters2 = new object[1] { method };
		return (Action<object, IndexT, object>)methodInfo.Invoke(null, parameters2);
	}

	private static Action<object, IndexT, object> CreateIndexerSetterHelper<InstanceT, IndexT, ValueT>(MethodInfo method)
	{
		Action<InstanceT, IndexT, ValueT> func = (Action<InstanceT, IndexT, ValueT>)method.CreateDelegate(typeof(Action<InstanceT, IndexT, ValueT>));
		return delegate(object target, IndexT index, object value)
		{
			func((InstanceT)target, index, ConvertValue<ValueT>(value));
		};
	}

	private static ValueT ConvertValue<ValueT>(object value)
	{
		Type typeFromHandle = typeof(ValueT);
		if (!typeFromHandle.GetTypeInfo().IsValueType)
		{
			return (ValueT)value;
		}
		return (ValueT)Convert.ChangeType(value, typeFromHandle);
	}

	private static Dictionary<Type, AddPropertyDelegate> AddPropertyFunctions()
	{
		return new Dictionary<Type, AddPropertyDelegate>
		{
			[typeof(bool)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<bool>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Bool);
			},
			[typeof(float)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<float>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Float);
			},
			[typeof(double)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<double>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Double);
			},
			[typeof(decimal)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor(info, p, (decimal v) => (double)v, (double v) => (decimal)v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Double);
			},
			[typeof(int)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<int>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Int);
			},
			[typeof(long)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<long>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Long);
			},
			[typeof(uint)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<uint>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.UInt);
			},
			[typeof(ulong)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<ulong>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.ULong);
			},
			[typeof(char)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor(info, p, (char v) => v, (uint v) => (char)v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.UInt);
			},
			[typeof(short)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<short>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Short);
			},
			[typeof(sbyte)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor(info, p, (sbyte v) => v, (short v) => (sbyte)v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Short);
			},
			[typeof(ushort)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<ushort>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.UShort);
			},
			[typeof(byte)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor(info, p, (byte v) => v, (ushort v) => (byte)v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.UShort);
			},
			[typeof(Color)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<Color>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Color);
			},
			[typeof(Point)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<Point>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Point);
			},
			[typeof(Rect)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<Rect>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Rect);
			},
			[typeof(Int32Rect)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<Int32Rect>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Int32Rect);
			},
			[typeof(Size)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<Size>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Size);
			},
			[typeof(Thickness)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<Thickness>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Thickness);
			},
			[typeof(CornerRadius)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<CornerRadius>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.CornerRadius);
			},
			[typeof(TimeSpan)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor(info, p, (TimeSpan v) => v, (TimeSpanStruct v) => v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.TimeSpan);
			},
			[typeof(Duration)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<Duration>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Duration);
			},
			[typeof(KeyTime)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<KeyTime>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.KeyTime);
			},
			[typeof(bool?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<bool?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableBool);
			},
			[typeof(float?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<float?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableFloat);
			},
			[typeof(double?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<double?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableDouble);
			},
			[typeof(decimal?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable(info, p, (decimal? v) => (double?)v, (double? v) => (decimal?)v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableDouble);
			},
			[typeof(int?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<int?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableInt);
			},
			[typeof(long?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<long?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableLong);
			},
			[typeof(uint?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<uint?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableUInt);
			},
			[typeof(ulong?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<ulong?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableULong);
			},
			[typeof(char?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable(info, p, (char? v) => v, (uint? v) => (char?)v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableUInt);
			},
			[typeof(short?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<short?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableShort);
			},
			[typeof(sbyte?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable(info, p, (sbyte? v) => v, (short? v) => (sbyte?)v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableShort);
			},
			[typeof(ushort?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<ushort?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableUShort);
			},
			[typeof(byte?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable(info, p, (byte? v) => v, (ushort? v) => (byte?)v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableUShort);
			},
			[typeof(Color?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<Color?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableColor);
			},
			[typeof(Point?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<Point?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullablePoint);
			},
			[typeof(Rect?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<Rect?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableRect);
			},
			[typeof(Int32Rect?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<Int32Rect?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableInt32Rect);
			},
			[typeof(Size?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<Size?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableSize);
			},
			[typeof(Thickness?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<Thickness?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableThickness);
			},
			[typeof(CornerRadius?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<CornerRadius?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableCornerRadius);
			},
			[typeof(TimeSpan?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable(info, p, (TimeSpan? v) => v, (TimeSpanStruct? v) => v, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableTimeSpan);
			},
			[typeof(Duration?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<Duration?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableDuration);
			},
			[typeof(KeyTime?)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessorNullable<KeyTime?>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.NullableKeyTime);
			},
			[typeof(string)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<string>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.String);
			},
			[typeof(Uri)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<Uri>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Uri);
			},
			[typeof(Type)] = delegate(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
			{
				AddPropertyAccessor<Type>(info, p, usePropertyInfo);
				return CreatePropertyData(p, NativePropertyType.Type);
			}
		};
	}

	private static bool IsCollectionType(Type type)
	{
		if (typeof(IList).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			return true;
		}
		if (typeof(IDictionary).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			return true;
		}
		if (FindListIndexer(type) != null)
		{
			return true;
		}
		if (FindDictIndexer(type) != null)
		{
			return true;
		}
		return false;
	}

	private static ExtendPropertyData AddProperty(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
	{
		if (_addPropertyFunctions.TryGetValue(p.PropertyType, out var value))
		{
			return value(info, p, usePropertyInfo);
		}
		if (p.PropertyType.GetTypeInfo().IsEnum)
		{
			if (IsSignedEnum(p.PropertyType))
			{
				AddPropertyAccessor(info, p, (object v) => (ulong)Convert.ToInt64(v), (ulong v) => Enum.ToObject(p.PropertyType, (long)v), usePropertyInfo: true);
			}
			else
			{
				AddPropertyAccessor(info, p, (object v) => Convert.ToUInt64(v), (ulong v) => Enum.ToObject(p.PropertyType, v), usePropertyInfo: true);
			}
			IntPtr propertyType = EnsureNativeType(p.PropertyType);
			return CreatePropertyData(p, NativePropertyType.Enum, propertyType);
		}
		AddPropertyAccessor<object>(info, p, usePropertyInfo: true);
		IntPtr intPtr = TryGetNativeType(p.PropertyType);
		if (intPtr == IntPtr.Zero)
		{
			intPtr = (IsCollectionType(p.PropertyType) ? EnsureNativeType(p.PropertyType) : BaseComponent.GetStaticType());
		}
		return CreatePropertyData(p, NativePropertyType.BaseComponent, intPtr);
	}

	private static bool IsSignedEnum(Type enumType)
	{
		Type enumUnderlyingType = enumType.GetTypeInfo().GetEnumUnderlyingType();
		if (!enumUnderlyingType.Equals(typeof(sbyte)) && !enumUnderlyingType.Equals(typeof(short)) && !enumUnderlyingType.Equals(typeof(int)))
		{
			return enumUnderlyingType.Equals(typeof(long));
		}
		return true;
	}

	private static void AddPropertyAccessor<PropertyT>(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
	{
		if (!usePropertyInfo)
		{
			AddPropertyAccessor(info, p, () => new PropertyAccessorRW<PropertyT>(p), () => new PropertyAccessorRO<PropertyT>(p));
		}
		else
		{
			AddPropertyAccessor(info, p, () => new PropertyAccessorPropRW<PropertyT>(p), () => new PropertyAccessorPropRO<PropertyT>(p));
		}
	}

	private static void AddPropertyAccessorNullable<PropertyT>(NativeTypePropsInfo info, PropertyInfo p, bool usePropertyInfo)
	{
		if (!usePropertyInfo)
		{
			AddPropertyAccessor(info, p, () => new PropertyAccessorNullableRW<PropertyT>(p), () => new PropertyAccessorNullableRO<PropertyT>(p));
		}
		else
		{
			AddPropertyAccessor(info, p, () => new PropertyAccessorNullablePropRW<PropertyT>(p), () => new PropertyAccessorNullablePropRO<PropertyT>(p));
		}
	}

	private static void AddPropertyAccessor<PropertyT, SourceT>(NativeTypePropsInfo info, PropertyInfo p, Func<SourceT, PropertyT> castTo, Func<PropertyT, SourceT> castFrom, bool usePropertyInfo)
	{
		if (!usePropertyInfo)
		{
			AddPropertyAccessor(info, p, () => new PropertyAccessorCastRW<PropertyT, SourceT>(p, new PropertyAccessorRW<SourceT>(p), castTo, castFrom), () => new PropertyAccessorCastRO<PropertyT, SourceT>(p, new PropertyAccessorRO<SourceT>(p), castTo));
		}
		else
		{
			AddPropertyAccessor(info, p, () => new PropertyAccessorCastRW<PropertyT, SourceT>(p, new PropertyAccessorPropRW<SourceT>(p), castTo, castFrom), () => new PropertyAccessorCastRO<PropertyT, SourceT>(p, new PropertyAccessorPropRO<SourceT>(p), castTo));
		}
	}

	private static void AddPropertyAccessorNullable<PropertyT, SourceT>(NativeTypePropsInfo info, PropertyInfo p, Func<SourceT, PropertyT> castTo, Func<PropertyT, SourceT> castFrom, bool usePropertyInfo)
	{
		if (!usePropertyInfo)
		{
			AddPropertyAccessor(info, p, () => new PropertyAccessorNullableCastRW<PropertyT, SourceT>(p, new PropertyAccessorRW<SourceT>(p), castTo, castFrom), () => new PropertyAccessorNullableCastRO<PropertyT, SourceT>(p, new PropertyAccessorRO<SourceT>(p), castTo));
		}
		else
		{
			AddPropertyAccessor(info, p, () => new PropertyAccessorNullableCastRW<PropertyT, SourceT>(p, new PropertyAccessorPropRW<SourceT>(p), castTo, castFrom), () => new PropertyAccessorNullableCastRO<PropertyT, SourceT>(p, new PropertyAccessorPropRO<SourceT>(p), castTo));
		}
	}

	private static void AddPropertyAccessor(NativeTypePropsInfo info, PropertyInfo p, Func<PropertyAccessor> creatorRW, Func<PropertyAccessor> creatorRO)
	{
		if (p.GetSetMethod() != null)
		{
			info.Properties.Add(creatorRW());
		}
		else
		{
			info.Properties.Add(creatorRO());
		}
	}

	private static IndexerAccessor CreateIndexerAccessor<IndexT>(PropertyInfo p)
	{
		if (p.GetSetMethod() != null)
		{
			if (!p.DeclaringType.GetTypeInfo().IsValueType)
			{
				return new IndexerAccessorRW<IndexT>(p);
			}
			return new IndexerAccessorPropRW<IndexT>(p);
		}
		if (!p.DeclaringType.GetTypeInfo().IsValueType)
		{
			return new IndexerAccessorRO<IndexT>(p);
		}
		return new IndexerAccessorPropRO<IndexT>(p);
	}

	private static ExtendPropertyData CreatePropertyData(PropertyInfo p, NativePropertyType extendType)
	{
		ExtendPropertyData result = new ExtendPropertyData
		{
			name = Marshal.StringToHGlobalAnsi(p.Name).ToInt64(),
			extendType = (int)extendType,
			readOnly = ((p.GetSetMethod() == null) ? 1 : 0)
		};
		TypeConverterAttribute customAttribute = p.GetCustomAttribute<TypeConverterAttribute>();
		if (customAttribute != null)
		{
			result.typeConverter = Marshal.StringToHGlobalAnsi(customAttribute.ConverterTypeName).ToInt64();
		}
		return result;
	}

	private static ExtendPropertyData CreatePropertyData(PropertyInfo p, NativePropertyType extendType, IntPtr propertyType)
	{
		ExtendPropertyData result = CreatePropertyData(p, extendType);
		result.type = propertyType.ToInt64();
		return result;
	}
}
