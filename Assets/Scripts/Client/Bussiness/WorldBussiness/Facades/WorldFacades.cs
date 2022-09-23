using Game.Infrastructure.Input;
using Game.Client.Bussiness.BattleBussiness.Controller.Domain;
using Game.Infrastructure.Network.Client;

namespace Game.Client.Bussiness.WorldBussiness.Facades
{

    public class WorldFacades
    {

        public AllWorldNetwork Network { get; private set; }

        public WorldFacades()
        {
            Network = new AllWorldNetwork();
        }

        public void Inject(NetworkClient client)
        {
            Network.Inject(client);
        }

        public void Init()
        {
        }

    }

}