using AutoMapper;
using JKClient;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JKWatcher
{
	class ServerInfoPublic
	{
		public DateTime? InfoPacketReceivedTime { get; set; } = null;
		public DateTime? StatusResponseReceivedTime { get; set; } = null;
		public bool InfoPacketReceived { get; set; } = false;
		public bool StatusResponseReceived { get; set; } = false; // If this is true, the Clients count is the actual count of clients excluding bots
		public bool NoBots { get; set; } = false;
		public string Address { get; set; }
		public string HostName { get; set; }
		public string MapName { get; set; }
		public string Game { get; set; }
		public string GameName { get; set; }
		public string GameType { get; set; }
		public int Clients { get; set; }
		public int ClientsIncludingBots { get; set; }
		public int MaxClients { get; set; }
		public int? PrivateClients { get; set; }
		public int MinPing { get; set; }
		public int MaxPing { get; set; }
		public int FPS { get; set; }
		public int Ping { get; set; }
		public bool Visibile { get; set; }
		public bool SendsAllEntities { get; set; }
		public bool NeedPassword { get; set; }
		public bool TrueJedi { get; set; }
		public bool WeaponDisable { get; set; }
		public bool ForceDisable { get; set; }
		public string Protocol { get; set; }
		public string Version { get; set; }
		public string ServerGameVersionString { get; set; }
		public string ServerSVInfoString { get; set; }
		public string Location { get; set; }
		public bool NWH { get; set; } // NWH mod detection
		public int FloodProtect { get; set; } = -1; // -1 if not yet set, -2 if server does not send it at all
		public bool Pure { get; set; }
		public string Players { get; set; }

		static IMapper mapper;
		static ServerInfoPublic() {
			MapperConfiguration configuration = new MapperConfiguration(cfg =>
			{
				//cfg.CreateMap<JKClient.ServerInfo, ServerInfoPublic>().ForMember(a=>a.Players,m=>m.MapFrom(src=>string.Join(',',src.Players)));
				cfg.CreateMap<JKClient.ServerInfo, ServerInfoPublic>().ForMember(a=>a.Players,m=>m.MapFrom(src=> JsonSerializer.Serialize(src.Players,(JsonSerializerOptions?) null)));
			});
#if DEBUG
			configuration.AssertConfigurationIsValid();
#endif
			mapper = configuration.CreateMapper();

		}

        ServerInfoPublic()
        {

        }
        public static ServerInfoPublic convertFromJKClient(ServerInfo serverInfo)
        {
			return mapper.Map<ServerInfoPublic>(serverInfo);
        }
	}
}
