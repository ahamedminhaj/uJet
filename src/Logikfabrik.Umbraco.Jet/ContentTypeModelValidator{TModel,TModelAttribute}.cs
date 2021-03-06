﻿// <copyright file="ContentTypeModelValidator{TModel,TModelAttribute}.cs" company="Logikfabrik">
//   Copyright (c) 2016 anton(at)logikfabrik.se. Licensed under the MIT license.
// </copyright>

namespace Logikfabrik.Umbraco.Jet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The <see cref="ContentTypeModelValidator{TModel,TModelAttribute}" /> class.
    /// </summary>
    /// <typeparam name="TModel">The <see cref="ContentTypeModel{T}" /> type.</typeparam>
    /// <typeparam name="TModelAttribute">The <see cref="ContentTypeModelAttribute" /> type.</typeparam>
    public class ContentTypeModelValidator<TModel, TModelAttribute> : TypeModelValidator<TModel, TModelAttribute>
        where TModel : ContentTypeModel<TModelAttribute>
        where TModelAttribute : ContentTypeModelAttribute
    {
        /// <summary>
        /// Validates the specified models.
        /// </summary>
        /// <param name="models">The models.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="models" /> is <c>null</c>.</exception>
        public override void Validate(TModel[] models)
        {
            if (models == null)
            {
                throw new ArgumentNullException(nameof(models));
            }

            ValidateById(models);
            ValidateByAlias(models);
            ValidatePropertiesById(models);
            ValidatePropertiesByAlias(models);
        }

        private static void ValidateByAlias(TModel[] models)
        {
            var set = new HashSet<string>();

            foreach (var model in models)
            {
                if (set.Contains(model.Alias, StringComparer.InvariantCultureIgnoreCase))
                {
                    var conflictingTypes = models.Where(m => m.Alias.Equals(model.Alias, StringComparison.InvariantCultureIgnoreCase)).Select(m => m.ModelType.Name);

                    throw new InvalidOperationException($"Alias conflict for types {string.Join(", ", conflictingTypes)}. Alias {model.Alias} is already in use.");
                }

                set.Add(model.Alias);
            }
        }

        private static void ValidatePropertiesById(TModel[] models)
        {
            foreach (var model in models)
            {
                ValidatePropertiesById(model);
            }
        }

        private static void ValidatePropertiesByAlias(TModel[] models)
        {
            foreach (var model in models)
            {
                ValidatePropertiesByAlias(model);
            }
        }

        private static void ValidatePropertiesById(TModel model)
        {
            var set = new HashSet<string>();

            foreach (var property in model.Properties)
            {
                if (set.Contains(property.Alias, StringComparer.InvariantCultureIgnoreCase))
                {
                    var conflictingProperties = model.Properties.Where(m => m.Alias.Equals(property.Alias, StringComparison.InvariantCultureIgnoreCase)).Select(m => m.Name);

                    throw new InvalidOperationException($"Alias conflict for properties {string.Join(", ", conflictingProperties)}. Alias {property.Alias} is already in use.");
                }

                set.Add(property.Alias);
            }
        }

        private static void ValidatePropertiesByAlias(TModel model)
        {
            var set = new HashSet<Guid>();

            foreach (var property in model.Properties)
            {
                if (!property.Id.HasValue)
                {
                    continue;
                }

                if (set.Contains(property.Id.Value))
                {
                    var conflictingProperties = model.Properties.Where(m => m.Id.HasValue && m.Id.Value == property.Id.Value).Select(m => m.Name);

                    throw new InvalidOperationException($"ID conflict for properties {string.Join(", ", conflictingProperties)}. ID {property.Id.Value} is already in use.");
                }

                set.Add(property.Id.Value);
            }
        }
    }
}
