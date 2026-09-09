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
	public partial class DakonStore : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public DakonStore() { }
		[Type(0, "uint16")]
		public ushort monocot = default(ushort);

		[Type(1, "uint16")]
		public ushort dicot = default(ushort);

		[Type(2, "uint16")]
		public ushort total = default(ushort);
	}
}
