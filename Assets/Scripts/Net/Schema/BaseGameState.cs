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
	public partial class BaseGameState : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public BaseGameState() { }
		[Type(0, "string")]
		public string phase = default(string);

		[Type(1, "string")]
		public string hostSessionId = default(string);

		[Type(2, "map", typeof(MapSchema<BasePlayer>))]
		public MapSchema<BasePlayer> players = null;
	}
}
