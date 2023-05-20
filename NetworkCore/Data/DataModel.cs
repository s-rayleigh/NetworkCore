using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ProtoBuf.Meta;

namespace NetworkCore.Data
{
	/// <summary>
	/// Model for serializing and deserializing the data packets.
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
				this.subtypes = new List<TypeContainer>();
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
					throw new ArgumentException($"Argument '{nameof(regType)}' must derive from class '{this.type}'.", nameof(regType));
				}
				
				this.model.RememberType(regType);
				
				var container = new TypeContainer(regType, this.model);
				this.subtypes.Add(container);
				return container;
			}
			
			internal void ClearSubtypes()
			{
				this.subtypes.Clear();
			}
		}

		private RuntimeTypeModel typeModel;

		/// <summary>
		/// Added packet types tree.
		/// </summary>
		protected readonly TypeContainer packetRoot;

		protected readonly TypeContainer containerRoot;

		protected readonly List<TypeContainer> structTypes;
		
		protected readonly List<Type> addedTypes;
		
		public DataModel()
		{
			this.packetRoot = new TypeContainer(typeof(Packet), this);
			this.containerRoot = new TypeContainer(typeof(Container), this);
			this.structTypes = new List<TypeContainer>();
			this.addedTypes = new List<Type>();
		}
		
		private void RememberType(Type type)
		{
			if(this.addedTypes.Contains(type))
			{
				throw new ArgumentException($"Type '{type}' is already added to the model.", nameof(type));
			}
			
			this.addedTypes.Add(type);
		}
		
		public TypeContainer AddPacket<T>() where T : Packet, new() => this.packetRoot.Add<T>();

		public TypeContainer AddContainer<T>() where T : Container, new() => this.containerRoot.Add<T>();

		public TypeContainer AddStruct<T>() where T : struct
		{
			var typeContainer = new TypeContainer(typeof(T), this);
			this.structTypes.Add(typeContainer);
			return typeContainer;
		}

		public TypeContainer Add(Type type)
		{
			if(type.IsSubclassOf(typeof(Packet)))
			{
				return this.packetRoot.Add(type);
			}
			
			if(type.IsSubclassOf(typeof(Container)))
			{
				return this.containerRoot.Add(type);
			}
			
			throw new ArgumentException($"Argument '{nameof(type)}' should derive from class '{typeof(Packet)}' or '{typeof(Container)}'.", nameof(type));
		}

		public void Build()
		{
			this.typeModel = TypeModel.Create();
			FillTypeModel(this.typeModel, this.containerRoot);
			FillTypeModel(this.typeModel, this.packetRoot);

			foreach(var typeContainer in this.structTypes)
			{
				FillTypeModel(this.typeModel, typeContainer);
			}
			
			this.typeModel.CompileInPlace();
		}

		public void Clear()
		{
			this.packetRoot.ClearSubtypes();
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

		public byte[] Serialize(Packet packet)
		{
			if(this.typeModel is null) this.Build();
			
			using(var ms = new MemoryStream())
			{
				// ReSharper disable once PossibleNullReferenceException
				this.typeModel.Serialize(ms, packet);
				return ms.ToArray();
			}
		}

		public Packet Deserialize(byte[] bytes)
		{
			// TODO: get rid of this check
			if(this.typeModel is null) this.Build();

			using(var ms = new MemoryStream(bytes))
			{
				// ReSharper disable once PossibleNullReferenceException
				return (Packet)this.typeModel.Deserialize(ms, null, typeof(Packet));
			}
		}
	}
}