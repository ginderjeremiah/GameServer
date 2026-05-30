# File Structure and Projects

The game is structured is similar to an onion architecture with multiple projects. The backend projects (all in folders of the same name) are:

- `Game.Abstractions`: This project contains shared abstractions and interfaces used across the backend, such as repositories, infrastructure services, and entity models.
- `Game.Api`: This project contains the ASP.NET Core Web API controllers, models, and related code for handling HTTP requests from the frontend, as well as WebSocket command handlers for real-time communication. It also includes a reflection-based code generator for automatically generating API client interfaces for the frontend based on the API controllers and WebSocket handlers.
- `Game.Api.Tests`: This project contains unit and integration tests for the `Game.Api` project, ensuring that all API endpoints and WebSocket handlers function correctly and that the battle logic is consistent with the frontend implementation.
- `Game.Application`: This project contains the application layer of the backend, including services for orchestrating game logic.
- `Game.Application.Tests`: This project contains unit and integration tests for the application layer, ensuring that all orchestration logic is correctly implemented and consistent with the frontend.
- `Game.Core`: This project contains the core domain logic of the game, including domain objects/services and the battle simulation logic. This is where the main game mechanics are implemented.
- `Game.Core.Tests`: This project contains unit tests for the core domain logic, ensuring that all game mechanics, including battle simulation, character progression, and item logic, are correctly implemented and consistent with the frontend.
- `Game.DataAccess`: This project contains the data access layer, including implementations of repositories from `Game.Abstractions` that interact with the database using Entity Framework and the cache and pub/sub implementations provided by `Game.Infrastructure`.
- `Game.Infrastructure`: This project contains the implementation of the cache and pub/sub interfaces defined in `Game.Abstractions` (currently using Redis), as well as the database context and migrations for EF Core.

# General Backend Guidelines

- Repositories should create and persist domain objects and are responsible for dispatching domain events before persisting changes. They should not contain any domain logic nor should they directly return entity models or DTOs. Mappers should be used to convert between domain objects, entity models, and DTOs and placed in the Mappers folder in the root of the `Game.DataAccess` project.
- All domain logic should be implemented in the `Game.Core` project, and the application layer should only be responsible for orchestrating calls to the repositories and domain objects/services to fulfill the use case. The API layer should only be responsible for handling HTTP/websocket requests, validating input, and returning responses, and should not contain any domain or orchestration logic.

## Testing Guidelines

Unit tests should be written according to the classical (Detroit) school of testing as much as possible. This project does not have any unmanaged dependencies, so you should be avoiding test doubles as much as possible in both unit and integration tests. If a service under test involves an out-of-process dependency such as the database or cache, it should only need to be tested through integration tests. Any classes that depend on out-of-process dependencies should NOT contain any logic worth unit testing. If you find that a service contains domain logic and depends on an out-of-process dependency, you should refactor the code to move said logic into an appropriate domain class that can be unit tested without the dependency, and then write integration tests only to validate the interaction with the out-of-process dependency.

# Important Architectural Design Decisions

## Reference Data

Reference data in this app can be split into 2 categories: intrinsic and static. Intrinsic reference data is fundamentally encoded into the application and often has an associated enum or similar construct in the code (e.g. `EAttribute` or `EItemCategory`). These are persisted into the database for data integrity, but they do not need to be (and very rarely are) queried from the database. Static reference data, on the other hand, is not fundamentally encoded into the application but is typically cached in-memory as a `List` such that the Id of the entity is its index. IMPORTANT: The Id column for these tables is seeded to start at 0 to facilitate this indexing. This includes things like items, item modifications, skills, enemies, zones, and challenges. This data is stored in the database and queried on application startup to be cached in-memory for fast access. Since this data is static and does not change often, it is not a problem to cache it in-memory, and it allows for much faster access than querying the database every time it is needed.

## HTTP vs WebSocket Communication

The backend uses a mix of HTTP and WebSockets for communication with the frontend. Right now, HTTP is used for some operations and data fetching, while WebSockets are used for real-time communication such as battle updates. In the future, the goal is to move towards using WebSockets for all communication (other than login/account creation) between the frontend and backend, as this will also enable the Player state to be kept in-memory on the backend and updated in real-time without having to worry about synchronizing with the redis cache or db. Users will only be able to connect to one instance at a time for simplicity and any websocket commands will be handled sequentially for each player. However, HTTP will still be used for admin tools and other operations that are not directly related to the core game.

## Caching and Pub/Sub

As mentioned above, most of the reference data is cached in-memory on the backend for fast access. However, player data is cached in Redis and uses a write-behind caching strategy where the cache is the source of truth for player data, and changes are persisted to the database asynchronously. The application uses Redis pub/sub to trigger persistence of player data to the database whenever it changes and as a backplane for WebSocket communication.
