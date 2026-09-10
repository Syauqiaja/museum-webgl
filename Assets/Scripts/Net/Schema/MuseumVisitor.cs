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
	public partial class MuseumVisitor : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public MuseumVisitor() { }
		[Type(0, "string")]
		public string displayName = default(string);

		[Type(1, "float32")]
		public float x = default(float);

		[Type(2, "float32")]
		public float y = default(float);

		[Type(3, "float32")]
		public float z = default(float);

		[Type(4, "float32")]
		public float yaw = default(float);

		[Type(5, "string")]
		public string avatar = default(string);

		[Type(6, "string")]
		public string activity = default(string);
	}
}
