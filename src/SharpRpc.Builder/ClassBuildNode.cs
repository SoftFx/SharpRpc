// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    public class ClassBuildNode
    {
        private readonly List<PropertyDeclarationSyntax> _properties = new List<PropertyDeclarationSyntax>();
        private readonly List<MethodDeclarationSyntax> _methods = new List<MethodDeclarationSyntax>();
        private readonly List<ClassBuildNode> _nestedClasses = new List<ClassBuildNode>();
        private readonly List<ClassBuildNode> _successors = new List<ClassBuildNode>();

        public ClassBuildNode(int key, TypeString className, TypeDeclarationSyntax declaration)
        {
            Key = key;
            ClassName = className;
            TypeDeclaration = declaration;
        }

        public int Key { get; }
        public TypeString ClassName { get; }
        public TypeDeclarationSyntax TypeDeclaration { get; private set; }
        public IReadOnlyList<PropertyDeclarationSyntax> PropertyDeclarations => _properties;
        public IReadOnlyList<MethodDeclarationSyntax> Methods => _methods;
        public IReadOnlyList<ClassBuildNode> NestedClasses => _nestedClasses;
        public IReadOnlyList<ClassBuildNode> Successors => _successors;

        public ClassBuildNode AddProperties(params PropertyDeclarationSyntax[] properties)
        {
            _properties.AddRange(properties);
            return this;
        }

        public ClassBuildNode AddProperties(IEnumerable<PropertyDeclarationSyntax> properties)
        {
            _properties.AddRange(properties);
            return this;
        }

        public ClassBuildNode AddMethods(params MethodDeclarationSyntax[] methods)
        {
            _methods.AddRange(methods);
            return this;
        }

        public ClassBuildNode AddMethods(IEnumerable<MethodDeclarationSyntax> methods)
        {
            _methods.AddRange(methods);
            return this;
        }

        public ClassBuildNode AddNestedClasses(IEnumerable<ClassBuildNode> nestedNodes)
        {
            foreach(var nestedNode in nestedNodes)
                _nestedClasses.Add(nestedNode);

            return this;
        }

        public ClassBuildNode AddNestedClass(ClassBuildNode node)
        {
            _nestedClasses.Add(node);
            return this;
        }

        public void AddBaseClass(ClassBuildNode baseClassNode)
        {
            baseClassNode._successors.Add(this);
        }

        public TypeDeclarationSyntax CompleteBuilding()
        {
            var completedNestedClasses = NestedClasses
                .Select(nc => nc.CompleteBuilding())
                .ToArray();

            return TypeDeclaration
                .AddMembers(_properties.ToArray())
                .AddMembers(_methods.ToArray())
                .AddMembers(completedNestedClasses);
        }

        public void UpdateDeclaration(Func<TypeDeclarationSyntax, TypeDeclarationSyntax> updateFunc)
        {
            TypeDeclaration = updateFunc(TypeDeclaration);
        }

        public void UpdatePropertyDeclaration(int index, Func<PropertyDeclarationSyntax, PropertyDeclarationSyntax> updateFunc)
        {
            _properties[index] = updateFunc(_properties[index]);
        }
    }
}
