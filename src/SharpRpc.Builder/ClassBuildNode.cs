// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    public class ClassBuildNode
    {
        private List<PropertyDeclarationSyntax> _properties;

        public ClassBuildNode(TypeString className, TypeDeclarationSyntax declaration, List<PropertyDeclarationSyntax> properties)
        {
            ClassName = className;
            TypeDeclaration = declaration;
            _properties = properties;
        }

        public ClassBuildNode(TypeString className, TypeDeclarationSyntax declaration, params PropertyDeclarationSyntax[] properties)
        {
            ClassName = className;
            TypeDeclaration = declaration;
            _properties = properties.ToList();
        }

        public TypeString ClassName { get; private set; }
        public TypeDeclarationSyntax TypeDeclaration { get; private set; }
        public IReadOnlyList<PropertyDeclarationSyntax> PropertyDeclarations => _properties;
        public List<ClassBuildNode> Successors { get; } = new List<ClassBuildNode>();

        public TypeDeclarationSyntax CompleteBuilding()
        {
            return TypeDeclaration.AddMembers(_properties.ToArray());
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
