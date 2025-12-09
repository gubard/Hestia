using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Gaia.Models;

namespace Hestia.Contract.Models;

[JsonSerializable(typeof(HestiaGetRequest))]
[JsonSerializable(typeof(HestiaPostRequest))]
[JsonSerializable(typeof(HestiaGetResponse))]
[JsonSerializable(typeof(HestiaPostResponse))]
[JsonSerializable(typeof(AlreadyExistsValidationError))]
[JsonSerializable(typeof(NotFoundValidationError))]
public partial class HestiaJsonContext : JsonSerializerContext
{
    public static readonly IJsonTypeInfoResolver Resolver;

    static HestiaJsonContext()
    {
        Resolver = Default.WithAddedModifier(typeInfo =>
        {
            if (typeInfo.Type == typeof(ValidationError))
            {
                typeInfo.PolymorphismOptions = new()
                {
                    TypeDiscriminatorPropertyName = "$type",
                    DerivedTypes =
                    {
                        new(typeof(AlreadyExistsValidationError),
                            typeof(AlreadyExistsValidationError).FullName!),
                        new(typeof(NotFoundValidationError),
                            typeof(NotFoundValidationError).FullName!),
                    },
                };
            }
        });
    }
}