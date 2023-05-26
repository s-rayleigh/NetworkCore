using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using ProtoBuf.Meta;

namespace NetworkCore.Data;

/// <summary>
/// Data model for serializing and deserializing messages.
/// </summary>
public class DataModel
{
	public class TypeContainer
	{
		/// <summary>
		/// Type that container holds.
		/// </summary>
		internal readonly Type type;

		/// <summary>
		/// Subtypes of this type.
		/// </summary>
		internal readonly List<TypeContainer> subtypes;

		/// <summary>
		/// Parent model.
		/// </summary>
		private readonly DataModel model;

		internal TypeContainer(Type type, DataModel model)
		{
			this.type = type;
			this.model = model;
			this.subtypes = new();
		}

		public TypeContainer Add<T>()
		{
			this.model.RememberType(typeof(T));
				
			var container = new TypeContainer(typeof(T), this.model);
			this.subtypes.Add(container);
			return container;
		}

		public TypeContainer Add(Type regType)
		{
			if(!regType.IsSubclassOf(this.type))
			{
				throw new ArgumentException($"Argument '{nameof(regType)}' must derive from class '{this.type}'.",
					nameof(regType));
			}
				
			this.model.RememberType(regType);
				
			var container = new TypeContainer(regType, this.model);
			this.subtypes.Add(container);
			return container;
		}

		internal void ClearSubtypes() => this.subtypes.Clear();
	}

	private RuntimeTypeModel typeModel;

	protected readonly TypeContainer messageRoot;

	protected readonly TypeContainer containerRoot;

	protected readonly List<TypeContainer> structTypes;
		
	protected readonly List<Type> addedTypes;
		
	public DataModel()
	{
		this.messageRoot = new(typeof(Message), this);
		this.containerRoot = new(typeof(Container), this);
		this.structTypes = new();
		this.addedTypes = new();
	}
		
	private void RememberType(Type type)
	{
		if(this.addedTypes.Contains(type))
		{
			throw new ArgumentException($"Type '{type}' is already added to the model.", nameof(type));
		}
			
		this.addedTypes.Add(type);
	}
		
	[PublicAPI]
	public TypeContainer AddMessage<T>() where T : Message, new() => this.messageRoot.Add<T>();

	[PublicAPI]
	public TypeContainer AddContainer<T>() where T : Container, new() => this.containerRoot.Add<T>();

	[PublicAPI]
	public TypeContainer AddStruct<T>() where T : struct
	{
		var typeContainer = new TypeContainer(typeof(T), this);
		this.structTypes.Add(typeContainer);
		return typeContainer;
	}

	public TypeContainer Add(Type type)
	{
		if(type.IsSubclassOf(typeof(Message))) return this.messageRoot.Add(type);
		if(type.IsSubclassOf(typeof(Container))) return this.containerRoot.Add(type);
		throw new ArgumentException(
			$"Argument '{nameof(type)}' should derive from class '{typeof(Message)}' or '{typeof(Container)}'.",
			nameof(type));
	}

	public void Build()
	{
		this.typeModel = TypeModel.Create();
		FillTypeModel(this.typeModel, this.containerRoot);
		FillTypeModel(this.typeModel, this.messageRoot);
		foreach(var typeContainer in this.structTypes) FillTypeModel(this.typeModel, typeContainer);
		this.typeModel.CompileInPlace();
	}

	public void Clear()
	{
		this.messageRoot.ClearSubtypes();
		this.containerRoot.ClearSubtypes();
		this.typeModel = null;
	}

	private static void FillTypeModel(RuntimeTypeModel model, TypeContainer container)
	{
		var fields = container.type
			.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
			.Select(field => field.Name).ToArray();
		var mt = model.Add(container.type, false).Add(fields);
		var i = fields.Length;
			
		foreach(var subtypeContainer in container.subtypes)
		{
			FillTypeModel(model, subtypeContainer);
			mt.AddSubType(++i, subtypeContainer.type);
		}
	}

	public byte[] Serialize(Message message)
	{
		if(this.typeModel is null) this.Build();
		using var ms = new MemoryStream();
		this.typeModel!.Serialize(ms, message);
		return ms.ToArray();
	}

	public Message Deserialize(byte[] bytes)
	{
		// TODO: get rid of this check.
		if(this.typeModel is null) this.Build();
		using var ms = new MemoryStream(bytes);
		return (Message)this.typeModel!.Deserialize(ms, null, typeof(Message));
	}
}