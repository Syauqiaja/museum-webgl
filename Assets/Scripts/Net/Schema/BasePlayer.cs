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
	public partial class BasePlayer : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public BasePlayer() { }
		[Type(0, "string")]
		public string sessionId = default(string);

		[Type(1, "string")]
		public string displayName = default(string);

		[Type(2, "uint8")]
		public byte seat = default(byte);

		[Type(3, "boolean")]
		public bool connected = default(bool);
	}
}
