---
name: dotnet-arch-entity
description: Guides the implementation of a new entity following Clean Architecture in a .NET 8 project. Covers all layers from DTO to Database, including EF Core Code First, mapping, Repository pattern, Domain services, DI registration, and Controller. Use when creating or modifying entities, adding new tables, or scaffolding CRUD features.
allowed-tools: Read, Grep, Glob, Bash, Write, Edit, Task
---

# .NET Clean Architecture — Entity Implementation Guide

You are an expert assistant that helps developers create or modify entities following Clean Architecture patterns in .NET 8 projects. You guide the user through ALL required layers.

## Input

The user will describe the entity to create or modify: `$ARGUMENTS`

Before generating code:
1. **Read the solution structure** — Identify all projects/layers (API, Domain, Infra, DTO, Application, etc.)
2. **Find an existing entity** — Use it as the primary reference to match patterns exactly (naming, namespaces, mapping approach, DI style)
3. **Read the DbContext** — Understand database provider (PostgreSQL, SQL Server, etc.), column naming conventions, and existing configurations
4. **Read the DI setup** — Find where services/repositories are registered (Program.cs, Startup.cs, Initializer.cs, etc.)
5. **Identify the mapping strategy** — AutoMapper profiles, manual mapping methods, or Mapster

---

## Architecture & Data Flow

```
Controller → Service → Repository → DbContext → Database
```

**Typical mapping chain:** EF Entity ↔ Domain Model ↔ DTO

**Common layered project structure:**

| Layer | Responsibility |
|-------|---------------|
| **DTO** | Public API contracts (request/response objects) |
| **Domain** | Entity interfaces, models, business logic, service interfaces |
| **Infra.Interfaces** | Repository contracts (optional — may live in Domain) |
| **Infra** | EF Core entities, DbContext, repositories, mapping profiles |
| **Application** | DI/IoC registration, cross-cutting concerns |
| **API** | Controllers, middleware, filters |

> **Adapt to the project:** Not all projects have every layer. Some combine Domain + Application, others have no separate Infra.Interfaces. Always match what already exists.

---

## Step-by-Step Implementation

### Step 1: DTO

Create the data transfer object(s) for the entity.

```csharp
namespace {DtoNamespace}
{
    public class {Entity}Info
    {
        public long {Entity}Id { get; set; }
        public string Name { get; set; }
        // Add fields based on user requirements
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
```

- Create separate DTOs if insert/update shapes differ: `{Entity}InsertInfo` (no Id), `{Entity}UpdateInfo` (with Id)
- Use nullable types for optional fields (`DateTime?`, `long?`)
- Match the naming convention of existing DTOs in the project

### Step 2: Domain Model

Create the domain entity. Adapt the pattern to what the project uses:

**Pattern A — Rich Domain Model (private setters, factory methods):**

```csharp
namespace {DomainNamespace}.Entities
{
    public class {Entity}Model
    {
        public long {Entity}Id { get; private set; }
        public string Name { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private {Entity}Model() { Name = string.Empty; }

        public static {Entity}Model Create(string name)
        {
            return new {Entity}Model
            {
                Name = name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void Update(string name)
        {
            Name = name;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

**Pattern B — Anemic Model (public setters, simple POCO):**

```csharp
namespace {DomainNamespace}.Entities
{
    public class {Entity}Model
    {
        public long {Entity}Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
```

- If the project uses **interfaces** for models (e.g., `I{Entity}Model`), create the interface too
- Match whichever pattern already exists in the project

### Step 3: EF Entity

Create the Entity Framework entity if the project separates EF entities from domain models:

```csharp
namespace {InfraNamespace}.Context
{
    public partial class {Entity}
    {
        public long {Entity}Id { get; set; }
        public string Name { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<RelatedEntity> RelatedEntities { get; set; } = new List<RelatedEntity>();
    }
}
```

> If the project uses the domain model directly as EF entity (no separate class), skip this step.

### Step 4: DbContext Configuration

Add `DbSet` and configure the entity in `OnModelCreating`:

```csharp
// Add DbSet
public virtual DbSet<{Entity}> {Entity}s { get; set; }

// Inside OnModelCreating — adapt conventions to match existing entities:
modelBuilder.Entity<{Entity}>(entity =>
{
    entity.HasKey(e => e.{Entity}Id);
    entity.ToTable("{table_name}"); // Match project's table naming convention

    entity.Property(e => e.{Entity}Id)
        .HasColumnName("{entity_id_column}");
    entity.Property(e => e.Name)
        .HasMaxLength(240)
        .HasColumnName("{name_column}");
    entity.Property(e => e.CreatedAt)
        .HasColumnName("{created_at_column}");
    entity.Property(e => e.UpdatedAt)
        .HasColumnName("{updated_at_column}");

    // Foreign keys, indexes, etc.
});
```

**Common conventions to detect:**
- Table naming: `snake_case` vs `PascalCase` vs plural vs singular
- Column naming: `snake_case` (`created_at`) vs `PascalCase` (`CreatedAt`)
- Primary key: auto-increment, sequences (`nextval`), or GUID
- Timestamps: `timestamp without time zone` (PostgreSQL) vs `datetime2` (SQL Server)
- FK behavior: `DeleteBehavior.Cascade` vs `ClientSetNull` vs `Restrict`

### Step 5: Migration

```bash
dotnet ef migrations add Add{Entity}Table --project {InfraProject} --startup-project {ApiProject}
dotnet ef database update --project {InfraProject} --startup-project {ApiProject}
```

### Step 6: Repository Interface

```csharp
namespace {RepoInterfaceNamespace}
{
    public interface I{Entity}Repository
    {
        IEnumerable<{Model}> ListAll();
        {Model} GetById(long id);
        {Model} Insert({Model} entity);
        {Model} Update({Model} entity);
        void Delete(long id);
    }
}
```

- Match the project's generic constraints if used (e.g., `I{Entity}Repository<TModel>`, `I{Entity}Repository<TModel, TFactory>`)
- Add pagination signatures if the project uses them: `(IEnumerable<T> Items, int TotalCount)`

### Step 7: Repository Implementation

```csharp
namespace {InfraNamespace}.Repository
{
    public class {Entity}Repository : I{Entity}Repository
    {
        private readonly {DbContextType} _context;

        public {Entity}Repository({DbContextType} context)
        {
            _context = context;
        }

        public IEnumerable<{Model}> ListAll()
        {
            return _context.{Entity}s
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .ToList();
        }

        public {Model} GetById(long id)
        {
            return _context.{Entity}s
                .AsNoTracking()
                .FirstOrDefault(e => e.{Entity}Id == id);
        }

        public {Model} Insert({Model} model)
        {
            _context.{Entity}s.Add(model);
            _context.SaveChanges();
            return model;
        }

        public {Model} Update({Model} model)
        {
            var existing = _context.{Entity}s.Find(model.{Entity}Id);
            if (existing == null) throw new KeyNotFoundException($"{Entity} not found.");
            // Update properties
            existing.Name = model.Name;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.SaveChanges();
            return existing;
        }

        public void Delete(long id)
        {
            var entity = _context.{Entity}s.Find(id);
            if (entity == null) throw new KeyNotFoundException($"{Entity} not found.");
            _context.{Entity}s.Remove(entity);
            _context.SaveChanges();
        }
    }
}
```

- If the project uses **AutoMapper** in repositories, inject `IMapper` and map EF ↔ Domain
- If the project uses **manual mapping** (DbToModel/ModelToDb methods), follow that pattern
- Match reads with `AsNoTracking()` if that's the convention
- Match timestamp handling for the database provider

### Step 8: Mapping (if applicable)

If the project uses **AutoMapper**, create profiles:

**EF Entity ↔ Domain Model profile:**
```csharp
public class {Entity}Profile : Profile
{
    public {Entity}Profile()
    {
        CreateMap<{EfEntity}, {DomainModel}>();
        CreateMap<{DomainModel}, {EfEntity}>()
            .ForMember(dest => dest.NavigationProp, opt => opt.Ignore());
    }
}
```

**Domain Model ↔ DTO profile:**
```csharp
public class {Entity}DtoProfile : Profile
{
    public {Entity}DtoProfile()
    {
        CreateMap<{DomainModel}, {Entity}Info>();
        CreateMap<{Entity}Info, {DomainModel}>();
    }
}
```

> Skip if the project uses manual mapping or has no separate EF entities.

### Step 9: Service Interface

```csharp
namespace {ServiceNamespace}.Interfaces
{
    public interface I{Entity}Service
    {
        IList<{Entity}Info> ListAll();
        {Entity}Info GetById(long id);
        {Entity}Info Insert({Entity}Info dto);
        {Entity}Info Update({Entity}Info dto);
        void Delete(long id);
    }
}
```

- Services receive/return **DTOs**, not domain models (unless the project does otherwise)
- Match the existing service interface patterns

### Step 10: Service Implementation

```csharp
namespace {ServiceNamespace}
{
    public class {Entity}Service : I{Entity}Service
    {
        private readonly I{Entity}Repository _repository;

        public {Entity}Service(I{Entity}Repository repository)
        {
            _repository = repository;
        }

        public IList<{Entity}Info> ListAll()
        {
            // Map from domain/EF to DTO (adapt mapping strategy)
            return _repository.ListAll().Select(e => new {Entity}Info
            {
                {Entity}Id = e.{Entity}Id,
                Name = e.Name,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            }).ToList();
        }

        public {Entity}Info GetById(long id)
        {
            var entity = _repository.GetById(id);
            if (entity == null) throw new KeyNotFoundException("{Entity} not found");
            // Map and return
        }

        public {Entity}Info Insert({Entity}Info dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            // Validate input
            // Map DTO → Domain, insert, map back
        }

        public {Entity}Info Update({Entity}Info dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            // Validate input
            // Map DTO → Domain, update, map back
        }

        public void Delete(long id) => _repository.Delete(id);
    }
}
```

- If the project uses `IMapper`, inject it and use `_mapper.Map<>()` instead of manual mapping
- Add validation in the service (or use FluentValidation if the project has it)
- Match error handling patterns (Exception types, error messages)

### Step 11: DI Registration

Find the DI registration file and add entries for the new repository and service:

```csharp
// Repository
services.AddScoped<I{Entity}Repository, {Entity}Repository>();

// Service
services.AddScoped<I{Entity}Service, {Entity}Service>();

// AutoMapper (if not already scanning the assembly)
services.AddAutoMapper(typeof({Entity}Profile).Assembly);
```

- Match the lifetime used by the project (Scoped, Transient, Singleton)
- Match the registration style (generic `AddScoped`, custom helper method, etc.)

### Step 12: Controller

```csharp
namespace {ApiNamespace}.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class {Entity}Controller : ControllerBase
    {
        private readonly I{Entity}Service _service;

        public {Entity}Controller(I{Entity}Service service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_service.ListAll());
        }

        [HttpGet("{id}")]
        public IActionResult GetById(long id)
        {
            try
            {
                return Ok(_service.GetById(id));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public IActionResult Insert([FromBody] {Entity}Info dto)
        {
            try
            {
                var result = _service.Insert(dto);
                return CreatedAtAction(nameof(GetById), new { id = result.{Entity}Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        public IActionResult Update([FromBody] {Entity}Info dto)
        {
            try
            {
                return Ok(_service.Update(dto));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(long id)
        {
            try
            {
                _service.Delete(id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }
}
```

- Match the existing route convention (`[Route("api/[controller]")]` vs `[Route("[controller]")]`)
- Match auth patterns (`[Authorize]`, user session retrieval, role checks)
- Match error response format (plain string, custom error DTO, etc.)
- Match HTTP verb conventions (POST for insert, PUT for update, etc.)

---

## Checklist

| # | Layer | Action | Description |
|---|-------|--------|-------------|
| 1 | DTO | Create | `{Entity}Info.cs` (and Insert/Update variants if needed) |
| 2 | Domain | Create | `{Entity}Model.cs` (and interface if project uses them) |
| 3 | Infra | Create | EF Entity `{Entity}.cs` (if separate from domain model) |
| 4 | Infra | Modify | DbContext — add `DbSet` and `OnModelCreating` configuration |
| 5 | Infra | Run | `dotnet ef migrations add Add{Entity}Table` |
| 6 | Domain/Infra | Create | `I{Entity}Repository.cs` interface |
| 7 | Infra | Create | `{Entity}Repository.cs` implementation |
| 8 | Infra | Create | Mapping profiles (if AutoMapper is used) |
| 9 | Domain | Create | `I{Entity}Service.cs` interface |
| 10 | Domain | Create | `{Entity}Service.cs` implementation |
| 11 | Application | Modify | DI registration (repository, service, mapper) |
| 12 | API | Create | `{Entity}Controller.cs` |

---

## Response Guidelines

1. **Read existing files first** — Find an existing complete entity and use it as the reference for all layers
2. **Follow the order** — DTO → Domain → Infra → Application → API
3. **Match all conventions** exactly — naming, namespaces, folder structure, code style
4. **Run migrations** after modifying DbContext
5. **Detect and match** the database provider conventions (column naming, timestamps, key generation)
6. **Adapt mapping strategy** — AutoMapper, manual mapping, or whatever the project uses
7. **Match DI patterns** — registration style and service lifetimes
8. **Match error handling** — exception types, response format, HTTP status codes
