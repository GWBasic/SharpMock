using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;

using Microsoft.CSharp;

namespace SharpMock
{
	public class InterfaceImplementor
	{
		public InterfaceImplementor(ModuleBuilder moduleBuilder)
		{
			this.moduleBuilder = moduleBuilder;
		}

		private readonly ModuleBuilder moduleBuilder;

		/// <summary>
		/// Creates a method that will generate an object that implements the interface for the 
		/// given type.
		/// </summary>
		/// <param name="type"></param>
		private void CreateTypeFor(Type type)
		{
			// Error checking...
			// Make sure that the type is an interface
			
			if (!type.IsInterface)
				throw new TypeIsNotAnInterface(type);

			var name = type.Name;
			if (name.StartsWith("I"))
				name = name.Substring(1);

			name = "Mock_" + name;

			name = type.Namespace + ".Mocks." + name;

			var typeBuilder = moduleBuilder.DefineType(
				name, TypeAttributes.Class | TypeAttributes.Public);
			typeBuilder.AddInterfaceImplementation(type);

			// Create Constructor
			var baseConstructorInfo = typeof(object).GetConstructor(new Type[0]);
			
			var constructorBuilder = typeBuilder.DefineConstructor(
				MethodAttributes.Public,
				CallingConventions.Standard,
				Type.EmptyTypes);
			
			var ilGenerator = constructorBuilder.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldarg_0);                      // Load "this"
			ilGenerator.Emit(OpCodes.Call, baseConstructorInfo);    // Call the base constructor
			ilGenerator.Emit(OpCodes.Ret);                          // return
			
			// Get a list of all methods, including methods in inherited interfaces
			// The methods that aren't accessors and will need default implementations...  However,
			// a property's accessors are also methods!
			var methods = new HashSet<MethodInfo>(this.RecursiveGetMethods(type));

			// Get a list of all of the properties, including properties in inherited interfaces
			var properties = new HashSet<PropertyInfo>(this.RecursiveGetProperties(type));

			// Get a list of all of the events, including events in inherited interfaces
			var events = new HashSet<PropertyInfo>(this.RecursiveGetProperties(type));

			// Create accessors for each property
			foreach (var propertyInfo in properties)
			{
				var propertyInfoName = propertyInfo.Name;
				var propertyType = propertyInfo.PropertyType;
				
				// Create underlying field; all properties have a field of the same type
				var field = typeBuilder.DefineField(
					"_" + propertyInfoName, propertyType, FieldAttributes.Private);
				
				// If there is a getter in the interface, create a getter in the new type
				var getMethod = propertyInfo.GetGetMethod();
				if (null != getMethod)
				{
					// This will prevent us from creating a default method for the property's 
					// getter
					methods.Remove(getMethod);
					
					// Now we will generate the getter method
					var methodBuilder = typeBuilder.DefineMethod(
						getMethod.Name, 
						MethodAttributes.Public | MethodAttributes.Virtual, 
						propertyType, 
						Type.EmptyTypes);
					
					// The ILGenerator class is used to put op-codes (similar to assembly) into the
					// method
					ilGenerator = methodBuilder.GetILGenerator();
					
					// These are the op-codes, (similar to assembly)
					ilGenerator.Emit(OpCodes.Ldarg_0);      // Load "this"
					ilGenerator.Emit(OpCodes.Ldfld, field); // Load the property's underlying field onto the stack
					ilGenerator.Emit(OpCodes.Ret);          // Return the value on the stack
					
					// We need to associate our new type's method with the getter method in the 
					// interface
					typeBuilder.DefineMethodOverride(methodBuilder, getMethod);
				}
				
				// If there is a setter in the interface, create a setter in the new type
				var setMethod = propertyInfo.GetSetMethod();
				if (null != setMethod)
				{
					// This will prevent us from creating a default method for the property's 
					// setter
					methods.Remove(setMethod);
					
					// Now we will generate the setter method
					var methodBuilder = typeBuilder.DefineMethod(
						setMethod.Name, 
						MethodAttributes.Public | MethodAttributes.Virtual, 
						typeof(void), 
						new Type[] { propertyInfo.PropertyType });
					
					// The ILGenerator class is used to put op-codes (similar to assembly) into the
					// method
					ilGenerator = methodBuilder.GetILGenerator();
					
					// These are the op-codes, (similar to assembly)
					ilGenerator.Emit(OpCodes.Ldarg_0);      // Load "this"
					ilGenerator.Emit(OpCodes.Ldarg_1);      // Load "value" onto the stack
					ilGenerator.Emit(OpCodes.Stfld, field); // Set the field equal to the "value" 
					// on the stack
					ilGenerator.Emit(OpCodes.Ret);          // Return nothing
					
					// We need to associate our new type's method with the setter method in the 
					// interface
					typeBuilder.DefineMethodOverride(methodBuilder, setMethod);
				}
			}
			
			// Create default methods.  These methods will essentially be no-ops; if there is a 
			// return value, they will either return a default value or null
			foreach (var methodInfo in methods)
			{
				// Get the return type and argument types
				
				var returnType = methodInfo.ReturnType;
				
				var argumentTypes = new List<Type>();
				foreach (var parameterInfo in methodInfo.GetParameters())
					argumentTypes.Add(parameterInfo.ParameterType);
				
				// Define the method
				var methodBuilder = typeBuilder.DefineMethod(
					methodInfo.Name, 
					MethodAttributes.Public | MethodAttributes.Virtual, 
					returnType, 
					argumentTypes.ToArray());
				
				// The ILGenerator class is used to put op-codes (similar to assembly) into the
				// method
				ilGenerator = methodBuilder.GetILGenerator();
				
				// If there's a return type, create a default value or null to return
				if (returnType != typeof(void))
				{
					var localBuilder = 
						ilGenerator.DeclareLocal(returnType);   // this declares the local object, 
					// int, long, float, ect
					ilGenerator.Emit(
						OpCodes.Ldloc, localBuilder);           // load the value on the stack to 
					// return
				}
				
				ilGenerator.Emit(OpCodes.Ret);                  // return
				
				// We need to associate our new type's method with the method in the interface
				typeBuilder.DefineMethodOverride(methodBuilder, methodInfo);
			}
			
			// TODO: Events
			foreach (var eventInfo in events)
			{
				GC.KeepAlive(eventInfo);
			}

			// Finally, after all the fields and methods are generated, create the type for use at
			// run-time
			typeBuilder.CreateType();
		}
		
		/// <summary>
		/// Helper method to get all MethodInfo objects from an interface.  This recurses to all 
		/// sub-interfaces
		/// </summary>
		/// <param name="methods"></param>
		/// <param name="type"></param>
		private IEnumerable<MethodInfo> RecursiveGetMethods(Type type)
		{
			foreach (var method in type.GetMethods())
				yield return method;
			
			foreach (var subInterface in type.GetInterfaces())
				foreach (var method in this.RecursiveGetMethods(subInterface))
					yield return method;
		}
		
		/// <summary>
		/// Helper method to get all PropertyInfo objects from an interface.  This recurses to all 
		/// sub-interfaces
		/// </summary>
		/// <param name="methods"></param>
		/// <param name="type"></param>
		private IEnumerable<PropertyInfo> RecursiveGetProperties(Type type)
		{
			foreach (var property in type.GetProperties())
				yield return property;
			
			foreach (var subInterface in type.GetInterfaces())
				foreach (var property in this.RecursiveGetProperties(subInterface))
					yield return property;
		}
		
		/// <summary>
		/// Helper method to get all EventInfo objects from an interface.  This recurses to all 
		/// sub-interfaces
		/// </summary>
		/// <param name="methods"></param>
		/// <param name="type"></param>
		private IEnumerable<EventInfo> RecursiveGetEvents(Type type)
		{
			foreach (var eventInfo in type.GetEvents())
				yield return eventInfo;
			
			foreach (var subInterface in type.GetInterfaces())
				foreach (var eventInfo in this.RecursiveGetEvents(subInterface))
					yield return eventInfo;
		}

		/// <summary>
		/// Thrown when an attempt is made to create an object of a type that is not an interface
		/// </summary>
		public class TypeIsNotAnInterface : ArgumentException
		{
			internal TypeIsNotAnInterface(Type type)
				: base("The InterfaceImplementor only works with interfaces. "
				       + "An attempt was made to create an object for the following type, " 
				       + "which is not an interface: " + type.FullName)
			{
				this.type = type;
			}

			public Type Type 
			{
				get { return type; }
			}
			private readonly Type type;
		}
	}
}

