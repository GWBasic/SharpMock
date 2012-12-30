using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SharpMock
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			if (2 != args.Length)
			{
				Console.WriteLine("The first argument is an assembly, and the second is the file to generate");
				return;
			}

			var assemblyFilename = args[0];
			var outputFilename = args[1];

			var assembly = Assembly.LoadFile(assemblyFilename);

			// Sort by namespace
			var interfacesByNamespace = new Dictionary<string, List<Type>>();

			foreach (var type in assembly.GetTypes().Where(type => type.IsInterface))
			{
				List<Type> interfaces;
				if (!interfacesByNamespace.TryGetValue(type.Namespace, out interfaces))
				{
					interfaces = new List<Type>();
					interfacesByNamespace[type.Namespace] = interfaces;
				}

				interfaces.Add(type);
			}

			using (var outputFilewriter = new StreamWriter(outputFilename))
			{
				new InterfaceImplementor().Implement(outputFilewriter, interfacesByNamespace);

				outputFilewriter.Flush();
			}
		}
	}
}
