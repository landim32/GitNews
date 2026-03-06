---
title: "O uso da Arquitetura Limpa (Clean Architecture) e Injeção de Dependências no projeto AiLogoMaker"
date: 2026-03-05T22:25:26Z
author: "Rodrigo Landim"
category: "Arquitetura"
tags:
  - "Clean Architecture"
  - "Injeção de Dependências"
  - "AiLogoMaker"
slug: "o-uso-da-arquitetura-limpa-clean-architecture-e-injecao-de-dependencias-no-projeto-ailogomaker"
---

Neste artigo, exploramos as novidades técnicas recentemente implementadas no projeto AiLogoMaker. As principais abordagens foram a aplicação da Arquitetura Limpa (Clean Architecture) e a Injeção de Dependências.

### Clean Architecture

A Arquitetura Limpa foi aplicada para separar as responsabilidades do domínio de negócio, aplicação e infraestrutura. Isso é evidente na estrutura do projeto, onde temos os projetos `AiLogoMaker.Domain` e `AiLogoMaker.Application`, cada um lidando com suas respectivas responsabilidades.

Um exemplo disso é o arquivo `AiLogoMaker.Application/Services/LogoOrchestrationService.cs`:

csharp
public class LogoOrchestrationService
{
    private readonly LogoBaseCreatorService _baseCreatorService;
    private readonly LogoVariantsCreatorService _variantsCreatorService;
    private readonly IosExportService _iosExportService;
    private readonly AndroidExportService _androidExportService;

    public LogoOrchestrationService(
        LogoBaseCreatorService baseCreatorService,
        LogoVariantsCreatorService variantsCreatorService,
        IosExportService iosExportService,
        AndroidExportService androidExportService)
    {
        _baseCreatorService = baseCreatorService;
        _variantsCreatorService = variantsCreatorService;
        _iosExportService = iosExportService;
        _androidExportService = androidExportService;
    }
}


Neste trecho, `LogoOrchestrationService` está seguindo o princípio de Inversão de Dependência, onde depende de abstrações e não de implementações concretas.

### Injeção de Dependências

A Injeção de Dependências é usada para fornecer as implementações necessárias em tempo de execução. Isso é evidente no arquivo `AiLogoMaker.Application/DependencyInjection.cs`, onde os serviços são registrados para injeção.

csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddSingleton<LogoOrchestrationService>();
    services.AddSingleton<LogoBaseCreatorService>();
    services.AddSingleton<LogoVariantsCreatorService>();
    services.AddSingleton<IosExportService>();
    services.AddSingleton<AndroidExportService>();

    return services;
}


Este trecho de código registra vários serviços como `Singleton` para serem injetados onde forem necessários.

### Conclusão

A aplicação da Arquitetura Limpa e a Injeção de Dependências ajudam a criar um código mais limpo, testável e de fácil manutenção. Através da análise dos commits recentes, foi possível observar que o projeto AiLogoMaker tem seguido essas práticas para melhorar a qualidade do código.
