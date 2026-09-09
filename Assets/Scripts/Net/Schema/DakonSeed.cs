// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 4.0.27
// 

using Colyseus.Schema;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Scripting;
#endif

namespace Museum.Net.State {
	public partial class DakonSeed : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public DakonSeed() { }
		[Type(0, "string")]
		public string id = default(string);

		[Type(1, "string")]
		public string category = default(string);

		[Type(2, "string")]
		public string typeId = default(string);
	}
}
