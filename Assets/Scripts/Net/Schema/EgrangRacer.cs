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
	public partial class EgrangRacer : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public EgrangRacer() { }
		[Type(0, "uint8")]
		public byte stick = default(byte);

		[Type(1, "uint16")]
		public ushort stepUnits = default(ushort);

		[Type(2, "uint8")]
		public byte place = default(byte);

		[Type(3, "boolean")]
		public bool ready = default(bool);
	}
}
