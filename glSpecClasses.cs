//
//  glSpecClasses.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2017 jp
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
using System.Collections.Generic;

namespace GLWrapperGen
{
	//classes holding gl.xml spec
	class glTypeDef {
		public string name;
		public string ctype;
		public string comment;
		public string requires;
		public string api;

		public override string ToString ()
		{
			return string.Format ("{0}:{1},{2}",
				api, ctype, name);
		}
	}
	class glEnumDef {
		public string ns;
		public string group;
		public string vendor;
		public bool bitmask = false;
		public List<glEnumValue> values = new List<glEnumValue>();

		public bool TryExtractExt(out string ext) {
			ext = "";
			foreach (string e in MainClass.extensions) {
				if (group.EndsWith (e)) {
					ext = e;
					return true;
				}					
			}
			return false;
		}

		public override string ToString ()
		{
			return string.Format ("{0}.{1} (2})", ns, group, vendor);
		}
	}
	class glEnumValue {
		public string api;
		public string name;
		public string value;
	}
	class glCommand {
		public string ns;
		public string returnType;
		public string name;
		public List<glParam> paramList = new List<glParam>(); 
	}
	class glParam {
		public string type;
		public string group;
		public string name;

		public override string ToString ()
		{
			return string.Format ("{0} {1}", type, name);
		}
	}
}

