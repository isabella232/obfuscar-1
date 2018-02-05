using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Obfuscar.Helpers
{
    static class TypeReferenceExtensions
    {
        /// <summary>
        /// Returns the simplified name for the assembly where a type can be found,
        /// for example, a type whose module is "Assembly.exe", "Assembly" would be 
        /// returned.
        /// </summary>
        public static string GetScopeName(this TypeReference type)
        {
            ModuleDefinition module = type.Scope as ModuleDefinition;
            if (module != null)
                return module.Assembly.Name.Name;
            else
                return type.Scope.Name;
        }

        public static string GetFullName(this TypeReference type)
        {
            string fullName = null;
            while (type.IsNested)
            {
                if (fullName == null)
                    fullName = type.Name;
                else
                    fullName = type.Name + "/" + fullName;
                type = type.DeclaringType;
            }

            if (fullName == null)
                fullName = type.Namespace + "." + type.Name;
            else
                fullName = type.Namespace + "." + type.Name + "/" + fullName;
            return fullName;
        }

        private class Substitution
        {
            public readonly String GenericParameterDeclaringTypeModuleFileName;
            public readonly MetadataToken GenericParameterDeclaringType;
            public readonly int GenericParameterPosition;
            public readonly TypeReference NewType;

            public Substitution(string genericParameterDeclaringTypeModuleFileName, MetadataToken genericParameterDeclaringType, int genericParameterPosition, TypeReference newType)
            {
                GenericParameterDeclaringTypeModuleFileName = genericParameterDeclaringTypeModuleFileName;
                GenericParameterDeclaringType = genericParameterDeclaringType;
                GenericParameterPosition = genericParameterPosition;
                NewType = newType;
            }
        }

        private static TypeReference SubstituteGenericArguments(this TypeReference type, List<Substitution> substitutions)
        {
            switch (type)
            {
                case GenericInstanceType genericInstanceType:
                    return genericInstanceType.ElementType
                        .SubstituteGenericArguments(substitutions)
                        .MakeGenericInstanceType(
                            genericInstanceType.GenericArguments
                            .Select(x => x.SubstituteGenericArguments(substitutions)).ToArray());
                case PointerType pointerType:
                    return pointerType.ElementType
                        .SubstituteGenericArguments(substitutions)
                        .MakePointerType();
                case ByReferenceType byReferenceType:
                    return byReferenceType.ElementType
                        .SubstituteGenericArguments(substitutions)
                        .MakeByReferenceType();
                case ArrayType arrayType:
                    return arrayType.Rank != 1
                        ? arrayType.ElementType
                            .SubstituteGenericArguments(substitutions)
                            .MakeArrayType(arrayType.Rank)
                        : arrayType.ElementType
                            .SubstituteGenericArguments(substitutions)
                            .MakeArrayType();
/*
                case FunctionPointerType functionPointerType:
                    return byReferenceType.ElementType
                        .SubstituteGenericArguments(substitutions)
                        .MakeByReferenceType();
*/

                case RequiredModifierType reqMod:
                    return reqMod.ElementType
                        .SubstituteGenericArguments(substitutions)
                        .MakeRequiredModifierType(
                            reqMod.ModifierType.SubstituteGenericArguments(substitutions));
                case OptionalModifierType optMod:
                    return optMod.ElementType
                        .SubstituteGenericArguments(substitutions)
                        .MakeOptionalModifierType(
                            optMod.ModifierType.SubstituteGenericArguments(substitutions));
                case SentinelType sentinelType:
                    return sentinelType.ElementType
                        .SubstituteGenericArguments(substitutions)
                        .MakeSentinelType();
                case PinnedType pinnedType:
                    return pinnedType.ElementType
                        .SubstituteGenericArguments(substitutions)
                        .MakePinnedType();
                case GenericParameter genericParameter:
                    var genericParameterDeclaringType = genericParameter.DeclaringType.Resolve();
                    
                    foreach (var substitution in substitutions)
                    {
                        if (substitution.GenericParameterPosition == genericParameter.Position &&
                            substitution.GenericParameterDeclaringType == genericParameterDeclaringType.MetadataToken &&
                            string.Equals(substitution.GenericParameterDeclaringTypeModuleFileName,
                                genericParameterDeclaringType.Module.FileName, StringComparison.Ordinal))
                        {
                            return substitution.NewType;
                        }
                    }

                    return genericParameter;
                default:
                    if (type.GetType() == typeof(TypeReference))
                    {
                        throw new Exception("Unhandled type reference: " + type.GetType().FullName);
                    }
                    else
                    {
                        throw new Exception("Unhandled type reference class: " + type.GetType().FullName);
                    }
            }
        }

        public static TypeReference SubstituteGenericArguments(this TypeReference type, GenericInstanceType substitutions)
        {
            if (substitutions.DeclaringType == null)
                Debugger.Break();

            var genericArgumentsCount = substitutions.GenericArguments.Count;
            
            if (substitutions.GenericParameters.Count != genericArgumentsCount)
                // TODO
                Debugger.Break();

            var typeDefinition = substitutions.ElementType.Resolve();
            if (typeDefinition == null)
                throw new Exception("Unresolved type: " + substitutions.ElementType.FullName);

            var substitutionsList = new List<Substitution>(genericArgumentsCount);
            for (int i = 0; i < genericArgumentsCount; i++)
            {
                var arg = substitutions.GenericArguments[i];
                
                substitutionsList[i] = new Substitution(
                    typeDefinition.Module.FileName,
                    typeDefinition.MetadataToken,
                    i,
                    arg);
            }

            return type.SubstituteGenericArguments(substitutionsList);
        }

    }
}
