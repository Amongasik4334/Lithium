using System;
using System.Collections.Generic;
using System.Linq;
using Lithium.Parser;
using Lithium.Bytegen;
using System.IO;

namespace Lithium
{
    internal class Program
    {
        static void Main(string[] args)
        {
            /*var i32_m = new Typeof.Atom("i32", true, 4, true);
            var i32_i = new Typeof.Atom("i32", false, 4, true);

            var main = new Declaration.Method("main", new List<Declaration.Local>(), i32_i, AccessMode.Public, new Block([
                new Variable(new Declaration.Local("x", i32_i, new Literal.Int(42))),
                new If(new Binary(new Literal.Int(1), Operator.Equal, new Literal.Int(1)),
                ),
                ]));

            var root = new Root([main]);
            TreeDebug(root);*/
            /*byte[] objBytes = Intelx86_64.GenerateExe("Hello!\r\n");
            File.WriteAllBytes("hello.exe", objBytes);
            Console.WriteLine("Файл hello.exe успешно создан!");*/
        }

        public static void TreeDebug(Node? node, string indent = "", bool last = true)
        {
            if (node == null) return;
            var props = node.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Where(p => p.Name is not ("Span" or "EqualityContract"));

            var scalars = props.Where(p => p.GetValue(node) is not (Node or (System.Collections.IEnumerable and not string)))
                .Select(p => $"{p.Name}: {(p.GetValue(node) is string s ? $"\"{s}\"" : p.GetValue(node) ?? "null")}");

            Console.WriteLine($"{indent}{(last ? "└── " : "├── ")}{node.GetType().Name}{(scalars.Any() ? $" ({string.Join(", ", scalars)})" : "")}");

            var kids = props.SelectMany(p => p.GetValue(node) switch { Node n => [n], System.Collections.IEnumerable l and not string => l.OfType<Node>(), _ => Array.Empty<Node>() }).ToArray();
            for (int i = 0; i < kids.Length; i++)
                TreeDebug(kids[i], indent + (last ? "    " : "│   "), i == kids.Length - 1);
        }
    }
}
