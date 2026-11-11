using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace VeAI.Api.Configuracao;

/// <summary>Documento OpenAPI com título, descrição e o esquema Bearer (botão "Authorize" do Swagger).</summary>
public static class OpenApiConfig
{
    private const string Esquema = "Bearer";

    public static IServiceCollection AddDocumentacaoOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Vê AI · API .NET";
                document.Info.Version = "v1";
                document.Info.Description =
                    "Assistente para pessoas com deficiência visual: descrição de imagens por IA (perigos primeiro) " +
                    "e relatos de barreiras de acessibilidade encaminhados ao gestor da região. Global Solution 2026/2 · FIAP.\n\n" +
                    "**Como testar:** faça `POST /auth/login` com `ana.souza@veai.dev` / `Veai@2026` (cidadã) ou " +
                    "`marina.alves@ong-veai.dev` / `Veai@2026` (gestora), copie o `token` e clique em **Authorize**.";

                // Grupos na ordem do fluxo do app, com uma frase sobre cada um.
                document.Tags = new HashSet<OpenApiTag>
                {
                    new() { Name = "Autenticação", Description = "Cadastro de cidadão e login (JWT)" },
                    new() { Name = "Usuário", Description = "Perfil e preferências de voz do usuário logado" },
                    new() { Name = "Regiões", Description = "Bairros atendidos pela rede de apoio" },
                    new() { Name = "Descrições", Description = "Descrição do ambiente por IA, perigos primeiro (a foto nunca é guardada)" },
                    new() { Name = "Relatos", Description = "Barreiras de acessibilidade relatadas pelo cidadão (regra dos 30 m e Idempotency-Key)" },
                    new() { Name = "Gestor", Description = "Fila da rede de apoio: só as regiões atendidas pelo gestor" },
                    new() { Name = "Notificações", Description = "Avisos gerados pelo consumidor do RabbitMQ e lidos em voz alta pelo app" }
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[Esquema] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Cole aqui o token recebido em POST /auth/login."
                };
                return Task.CompletedTask;
            });

            // Toda rota que não é anônima exige o token: o Swagger mostra o cadeado e envia o header.
            options.AddOperationTransformer((operation, context, _) =>
            {
                if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
                    return Task.CompletedTask;

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(Esquema, context.Document)] = []
                });
                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Sem token ou token inválido/expirado." });
                return Task.CompletedTask;
            });
        });
}
