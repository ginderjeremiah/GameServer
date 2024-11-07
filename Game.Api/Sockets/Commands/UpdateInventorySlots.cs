using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Api.Services;
using Game.Core.Sessions;

namespace Game.Api.Sockets.Commands
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
            if (Session.TryUpdateInventoryItems(Parameters.Cast<IInventoryUpdate>()))
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
