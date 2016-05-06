//
//  GLSpecParser.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2016 jp
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Xml;

namespace GLWrapperGen
{
	public class GLRegistery
	{
		public GLGroup[] Groups;

		#region Registery loading
		public static GLRegistery Load(string xmlSpecPath)
		{
			GLRegistery registery = new GLRegistery ();

			XmlDocument glspec = new XmlDocument();
			glspec.Load ("gl.xml");

			registery.Groups = loadGroups (glspec.SelectSingleNode ("/registry/groups"));

		}

		static void loadGroups(XmlNode groups){

			foreach (XmlNode group in groups) {
				string enumName = group.Attributes ["name"]?.Value;
				if (string.IsNullOrEmpty (enumName))
					continue;
				CodeTypeDeclaration e = new CodeTypeDeclaration (enumName);
				e.IsEnum = true;
				foreach (XmlNode v in group.ChildNodes) {
					string enumItem = ToCameCase (v.Attributes ["name"]?.Value);
					if (string.IsNullOrEmpty (enumItem))
						continue;					
					CodeMemberField ev = new CodeMemberField (enumName, enumItem);
					e.Members.Add (ev);
				}
				BaseNameSpace.Types.Add (e);
			}

			XmlNodeList enums = glspec.SelectNodes("/registry/enums");

			foreach (XmlNode en in enums) {
				string enumName = en.Attributes ["group"]?.Value;
				if (string.IsNullOrEmpty (enumName) || string.Equals("SpecialNumbers",enumName)) {
					//value has to be set for each occurence
					foreach (XmlNode v in en.SelectNodes("enum")) {
						string initValue = v.Attributes ["value"]?.Value;
						string enumItem = ToCameCase (v.Attributes ["name"]?.Value);
						foreach (CodeTypeDeclaration ctd in BaseNameSpace.Types) {
							CodeMemberField ev = FindEnumByName (ctd, enumItem);
							if (ev == null)
								continue;
							ev.InitExpression = new CodeSnippetExpression (initValue);							
						}
					}
					continue;	
				}
				CodeTypeDeclaration e = FindEnumByName(BaseNameSpace, enumName, true);
				foreach (XmlNode v in en.SelectNodes("enum")) {
					string enumItem = ToCameCase (v.Attributes ["name"]?.Value);
					CodeMemberField ev = FindEnumByName (e, enumItem, true);
					ev.InitExpression = new CodePrimitiveExpression (v.Attributes ["value"]?.Value);
				}
			}

			GenerateCSharpCode (codeBase, @"generated/enums.cs");
		}

		static void processCommands(XmlDocument glspec){
			CodeCompileUnit codeBase = new CodeCompileUnit ();
			CodeNamespace BaseNameSpace = new CodeNamespace (strBaseNameSpace);

			codeBase.Namespaces.Add (new CodeNamespace ());
			codeBase.Namespaces.Add (BaseNameSpace);

			//using 
			codeBase.Namespaces [0].Imports.Add (new CodeNamespaceImport ("System"));

			CodeTypeDeclaration gl = new CodeTypeDeclaration ("GL");
			gl.IsClass = true;
			gl.IsPartial = true;
			gl.Attributes = MemberAttributes.Public | MemberAttributes.Static;
			BaseNameSpace.Types.Add (gl);

			XmlNode cmds = glspec.SelectSingleNode("/registry/commands");

			foreach (XmlNode cmd in cmds.ChildNodes) {
				XmlNode proto = cmd.SelectSingleNode("proto"); 
				CodeMemberMethod p = new CodeMemberMethod ();
				p.Attributes = MemberAttributes.Public | MemberAttributes.Static;

				XmlNode returnTypeNode = proto.SelectSingleNode ("ptype");
				if (returnTypeNode != null) {
					string rt = returnTypeNode.InnerXml;
					if (rt == "GLenum") {
						if (proto.Attributes ["group"] != null) {
							p.ReturnType = new CodeTypeReference (proto.Attributes ["group"].Value);
						} else
							p.ReturnType = new CodeTypeReference ("System.UInt32");

					} else {
						if (GLTypeDic.ContainsKey (rt))
							p.ReturnType = new CodeTypeReference (GLTypeDic [rt]);
						else
							Console.WriteLine (rt);
					}
				}
				string cmdName = proto.SelectSingleNode ("name").InnerXml.Substring (2);
				if (cmdName.EndsWith ("NV"))
					cmdName.Substring (0, cmdName.Length - 2);

				p.Name = cmdName;
				gl.Members.Add (p);

			}				

			GenerateCSharpCode (codeBase, @"generated/gl.cs");
		}
		#endregion
	}
}

