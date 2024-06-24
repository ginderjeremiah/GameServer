using GameCore;
using GameCore.Sessions;

namespace GameServer.Services
{
    public class SessionService(IRepositoryManager repos)
    {
        private Session? _session;
        private readonly IRepositoryManager _repos = repos;

        public bool SessionAvailable => _session is not null;

        public Session GetSession()
        {
            return _session ?? throw new SessionNotInitializedException();
        }

        public async Task<Session?> LoadSession(string sessionId)
        {
            var sessionData = await _repos.SessionStore.GetSessionAsync(sessionId);
            if (sessionData == null)
            {
                return null;
            }
            else
            {
                return _session = new Session(sessionData, _repos);
            }
        }
    }

    public class SessionNotInitializedException : Exception
    {
        public SessionNotInitializedException() : base("The session was not intialized.") { }
    }
}
