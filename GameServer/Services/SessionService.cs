using GameCore;
using GameCore.Sessions;

namespace GameServer.Services
{
    /// <summary>
    /// A service for loading <see cref="Session"/> data for a request.
    /// </summary>
    /// <param name="repos">The <see cref="IRepositoryManager"/> which is used to load session data.</param>
    public class SessionService(IRepositoryManager repos)
    {
        private Session? _session;
        private readonly IRepositoryManager _repos = repos;

        /// <summary>
        /// Indicates whether a <see cref="Session"/> has been loaded into the <see cref="SessionService"/> or not.
        /// </summary>
        public bool SessionAvailable => _session is not null;

        /// <summary>
        /// Gets the currently loaded <see cref="Session"/>.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="SessionNotInitializedException"></exception>
        public Session GetSession()
        {
            return _session ?? throw new SessionNotInitializedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Session?> LoadSession(string sessionId)
        {
            var sessionData = await _repos.SessionStore.GetSessionAsync(sessionId);
            return sessionData == null ? null : (_session = new Session(sessionData, _repos));
        }
    }

    /// <summary>
    /// An <see cref="Exception"/> which is generated when a <see cref="SessionService"/> does not have a <see cref="Session"/> loaded.
    /// </summary>
    public class SessionNotInitializedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SessionNotInitializedException"/> class with the default error message.
        /// </summary>
        public SessionNotInitializedException() : base("The session was not intialized.") { }
    }
}
