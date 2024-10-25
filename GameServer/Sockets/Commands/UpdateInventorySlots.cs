using GameCore.Sessions;
using GameServer.Models.Common;
using GameServer.Models.InventoryItems;
using GameServer.Services;

namespace GameServer.Sockets.Commands
{
    public class UpdateInventorySlots : AbstractSocketCommandWithParams<List<InventoryUpdate>>
    {
        private Session Session { get; }

        public UpdateInventorySlots(SessionService sessionService)
        {
            Session = sessionService.GetSession();
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context)
        {
            if (Session.TryUpdateInventoryItems(Parameters.Cast<IInventoryUpdate>().ToList()))
            {
                await Session.Save();
                return Success();
            }
            else
            {
                return Error("Unable to update inventory items.");
            }
        }
    }
}
