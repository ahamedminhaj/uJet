﻿// <copyright file="DocumentTypeSynchronizationService.cs" company="Logikfabrik">
//   Copyright (c) 2015 anton(at)logikfabrik.se. Licensed under the MIT license.
// </copyright>

namespace Logikfabrik.Umbraco.Jet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Data;
    using global::Umbraco.Core;
    using global::Umbraco.Core.Logging;
    using global::Umbraco.Core.Models;
    using global::Umbraco.Core.ObjectResolution;
    using global::Umbraco.Core.Services;

    /// <summary>
    /// The <see cref="DocumentTypeSynchronizationService" /> class. Synchronizes types annotated using the <see cref="DocumentTypeAttribute" />.
    /// </summary>
    public class DocumentTypeSynchronizationService : ContentTypeSynchronizationService
    {
        private readonly ITypeService _typeService;
        private readonly IFileService _fileService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentTypeSynchronizationService" /> class.
        /// </summary>
        public DocumentTypeSynchronizationService()
            : this(
                ApplicationContext.Current.Services.ContentTypeService,
                new ContentTypeRepository(new DatabaseWrapper(ApplicationContext.Current.DatabaseContext.Database, ResolverBase<LoggerResolver>.Current.Logger, ApplicationContext.Current.DatabaseContext.SqlSyntax)),
                TypeService.Instance,
                ApplicationContext.Current.Services.FileService)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentTypeSynchronizationService" /> class.
        /// </summary>
        /// <param name="contentTypeService">The content type service.</param>
        /// <param name="contentTypeRepository">The content type repository.</param>
        /// <param name="typeService">The type service.</param>
        /// <param name="fileService">The file service.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="typeService" />, or <paramref name="fileService" /> are <c>null</c>.</exception>
        public DocumentTypeSynchronizationService(
            IContentTypeService contentTypeService,
            IContentTypeRepository contentTypeRepository,
            ITypeService typeService,
            IFileService fileService)
            : base(contentTypeService, contentTypeRepository)
        {
            if (typeService == null)
            {
                throw new ArgumentNullException(nameof(typeService));
            }

            if (fileService == null)
            {
                throw new ArgumentNullException(nameof(fileService));
            }

            _fileService = fileService;
            _typeService = typeService;
        }

        /// <summary>
        /// Synchronizes this instance.
        /// </summary>
        public override void Synchronize()
        {
            var documentTypes = _typeService.DocumentTypes.Select(t => new DocumentType(t)).ToArray();

            // No document types; there's nothing to sync.
            if (!documentTypes.Any())
            {
                return;
            }

            ValidateDocumentTypeId(documentTypes);
            ValidateDocumentTypeAlias(documentTypes);

            // WARNING: This might cause issues; the array of types only contains the initial types, not including ones added/updated during sync.
            var types = ContentTypeService.GetAllContentTypes().ToArray();

            foreach (var documentType in documentTypes.Where(dt => dt.Id.HasValue))
            {
                SynchronizeById(types, documentType);
            }

            foreach (var documentType in documentTypes.Where(dt => !dt.Id.HasValue))
            {
                SynchronizeByAlias(types, documentType);
            }

            SetAllowedContentTypes(types.Cast<IContentTypeBase>().ToArray(), documentTypes);
        }

        /// <summary>
        /// Synchronizes document type by alias.
        /// </summary>
        /// <param name="contentTypes">The content types.</param>
        /// <param name="documentType">The document type.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="contentTypes" />, or <paramref name="documentType" /> are <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the document type identifier is not <c>null</c>.</exception>
        internal virtual void SynchronizeByAlias(IEnumerable<IContentType> contentTypes, DocumentType documentType)
        {
            if (contentTypes == null)
            {
                throw new ArgumentNullException(nameof(contentTypes));
            }

            if (documentType == null)
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            if (documentType.Id.HasValue)
            {
                throw new ArgumentException("Document type ID must be null.", nameof(documentType));
            }

            var ct = contentTypes.FirstOrDefault(type => type.Alias == documentType.Alias);

            if (ct == null)
            {
                CreateDocumentType(documentType);
            }
            else
            {
                UpdateDocumentType(ct, documentType);
            }
        }

        /// <summary>
        /// Synchronizes document type by identifier.
        /// </summary>
        /// <param name="contentTypes">The content types.</param>
        /// <param name="documentType">The document type.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="contentTypes" />, or <paramref name="documentType" /> are <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the document type identifier is <c>null</c>.</exception>
        internal virtual void SynchronizeById(IEnumerable<IContentType> contentTypes, DocumentType documentType)
        {
            if (contentTypes == null)
            {
                throw new ArgumentNullException(nameof(contentTypes));
            }

            if (documentType == null)
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            if (!documentType.Id.HasValue)
            {
                throw new ArgumentException("Document type ID cannot be null.", nameof(documentType));
            }

            IContentType ct = null;

            var id = ContentTypeRepository.GetContentTypeId(documentType.Id.Value);

            if (id.HasValue)
            {
                // The document type has been synchronized before. Get the matching content type.
                // It might have been removed using the back office.
                ct = contentTypes.FirstOrDefault(type => type.Id == id.Value);
            }

            if (ct == null)
            {
                CreateDocumentType(documentType);

                // Get the created content type.
                ct = ContentTypeService.GetContentType(documentType.Alias);

                // Connect the document type and the created content type.
                ContentTypeRepository.SetContentTypeId(documentType.Id.Value, ct.Id);
            }
            else
            {
                UpdateDocumentType(ct, documentType);
            }
        }

        /// <summary>
        /// Updates the document type.
        /// </summary>
        /// <param name="contentType">The content type.</param>
        /// <param name="documentType">The document type to update.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="contentType" />, or <paramref name="documentType" /> are <c>null</c>.</exception>
        internal virtual void UpdateDocumentType(IContentType contentType, DocumentType documentType)
        {
            if (contentType == null)
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            if (documentType == null)
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            UpdateContentType(contentType, () => new global::Umbraco.Core.Models.ContentType(-1), documentType);
            SetTemplates(contentType, documentType);
            SetDefaultTemplate(contentType, documentType);

            ContentTypeService.Save(contentType);

            // Update tracking.
            SetPropertyTypeId(ContentTypeService.GetContentType(contentType.Alias), documentType);
        }

        /// <summary>
        /// Validates the document type identifier.
        /// </summary>
        /// <param name="documentTypes">The document types.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="documentTypes" /> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if an identifier in <paramref name="documentTypes" /> is conflicting.</exception>
        private static void ValidateDocumentTypeId(IEnumerable<DocumentType> documentTypes)
        {
            if (documentTypes == null)
            {
                throw new ArgumentNullException(nameof(documentTypes));
            }

            var set = new HashSet<Guid>();

            foreach (var documentType in documentTypes)
            {
                if (!documentType.Id.HasValue)
                {
                    continue;
                }

                if (set.Contains(documentType.Id.Value))
                {
                    throw new InvalidOperationException(
                        $"ID conflict for document type {documentType.Name}. ID {documentType.Id.Value} is already in use.");
                }

                set.Add(documentType.Id.Value);
            }
        }

        /// <summary>
        /// Validates the document type alias.
        /// </summary>
        /// <param name="documentTypes">The document types.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="documentTypes" /> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if an alias in <paramref name="documentTypes" /> is conflicting.</exception>
        private static void ValidateDocumentTypeAlias(IEnumerable<DocumentType> documentTypes)
        {
            if (documentTypes == null)
            {
                throw new ArgumentNullException(nameof(documentTypes));
            }

            var set = new HashSet<string>();

            foreach (var documentType in documentTypes)
            {
                if (set.Contains(documentType.Alias))
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Alias conflict for document type {0}. Alias {0} is already in use.",
                            documentType.Alias));
                }

                set.Add(documentType.Alias);
            }
        }

        /// <summary>
        /// Creates the document type.
        /// </summary>
        /// <param name="documentType">The document type to create.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="documentType" /> is <c>null</c>.</exception>
        private void CreateDocumentType(DocumentType documentType)
        {
            if (documentType == null)
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            var t = (IContentType)CreateContentType(() => new global::Umbraco.Core.Models.ContentType(-1), documentType);

            SetTemplates(t, documentType);
            SetDefaultTemplate(t, documentType);

            ContentTypeService.Save(t);

            // Update tracking.
            SetPropertyTypeId(ContentTypeService.GetContentType(t.Alias), documentType);
        }

        /// <summary>
        /// Sets the templates.
        /// </summary>
        /// <param name="contentType">The content type.</param>
        /// <param name="documentType">The document type.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="contentType" />, or <paramref name="documentType" /> are <c>null</c>.</exception>
        private void SetTemplates(IContentType contentType, DocumentType documentType)
        {
            if (contentType == null)
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            if (documentType == null)
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            IEnumerable<ITemplate> templates = new ITemplate[] { };

            if (documentType.Templates != null && !documentType.Templates.Any())
            {
                templates =
                    _fileService.GetTemplates(documentType.Templates.ToArray()).Where(template => template != null);
            }

            contentType.AllowedTemplates = templates;
        }

        /// <summary>
        /// Sets the default template.
        /// </summary>
        /// <param name="contentType">The content type.</param>
        /// <param name="documentType">The document type.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="contentType" />, or <paramref name="documentType" /> are <c>null</c>.</exception>
        private void SetDefaultTemplate(IContentType contentType, DocumentType documentType)
        {
            if (contentType == null)
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            if (documentType == null)
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            ITemplate template = null;

            if (!string.IsNullOrWhiteSpace(documentType.DefaultTemplate))
            {
                template = _fileService.GetTemplate(documentType.DefaultTemplate);
            }

            contentType.SetDefaultTemplate(template);
        }
    }
}
