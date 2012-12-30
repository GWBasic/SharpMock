using System;
using System.Collections.Generic;
using System.IO;

namespace SharpMock
{
	public class InterfaceImplementor
	{
		public InterfaceImplementor ()
		{
		}

		public void Implement(StreamWriter streamWriter, Dictionary<string, List<Type>> interfacesByNamespace)
		{
			foreach (var kvp in interfacesByNamespace)
			{
				var namespaceS = kvp.Key;
				var interfaces = kvp.Value;

				streamWriter.WriteLine("namespace {0}.Mocks", namespaceS);
				streamWriter.WriteLine("{");

				foreach (var type in interfaces)
				{
					this.ImplementInterface(streamWriter, type);
				}

				streamWriter.WriteLine("}\n");
			}
		}

		public void ImplementInterface(StreamWriter streamWriter, Type type)
		{
			streamWriter.WriteLine(
				"\tpublic class {0}_Mock : {1}",
				type.Name,
				type.FullName);
			streamWriter.WriteLine("\t{");

			foreach (var method in type.GetMethods())
			{
				streamWriter.Write(
					"\t\tpublic {0} {1}(",
					method.ReturnType != null ? method.ReturnType.FullName : "void",
					method.Name);

				streamWriter.WriteLine(")");
			}

			streamWriter.WriteLine("\t}\n");
		}
	}
}

