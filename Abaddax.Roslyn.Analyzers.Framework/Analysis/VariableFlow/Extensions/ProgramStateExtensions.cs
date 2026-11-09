using Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models;
using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Abaddax.Roslyn.Analyzers.Framework.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Extensions
{
    internal static class ProgramStateExtensions
    {
        /// <summary>
        /// Allocates a new variable on the stack (value types) or heap (reference types)
        /// </summary>
        /// <example>
        /// Operation: x = new Type();
        /// </example>
        public static ProgramState Allocate(this ProgramState state, AccessPathRoot variableSymbol, ITypeSymbol objectType, IOperation? allocationOperation)
        {
            var value = AllocateDefaultValueForType(ref state, objectType, allocationOperation);
            return state.WithEnvironment(variableSymbol, value);
        }

        /// <summary>
        /// Assigns the value of <paramref name="sourcePath"/> to <paramref name="targetPath"/>
        /// </summary>
        /// <remarks>
        /// Automatically handles reference aliasing vs value-type cloning
        /// </remarks>
        /// <example>
        /// y = x; x.Prop = y.Prop;:
        /// </example>
        public static ProgramState Assign(this ProgramState state, AccessPath targetPath, AccessPath sourcePath)
        {
            var sourceVal = EvaluatePath(ref state, sourcePath);
            var copiedVal = sourceVal.Copy();
            return state.Write(targetPath, copiedVal);
        }

        /// <summary>
        /// Writes a <paramref name="value"/> to the given <paramref name="path"/> 
        /// </summary>
        /// <remarks>
        /// Automatically routing between stack/heap variables and nested fields
        /// </remarks>
        public static ProgramState Write(this ProgramState state, AccessPath path, IAbstractValue value)
        {
            if (path.Members.IsEmpty)
                //Root-level assignment
                return state.WithEnvironment(path.Root, value);

            var parentPathMembers = path.Members.RemoveAt(path.Members.Length - 1);
            var targetMember = path.Members.Last();

            var parentPath = AccessPath.FromRoot(path.Root).AppendRange(parentPathMembers);
            var parentVal = EvaluatePath(ref state, parentPath);
            return parentVal switch
            {
                ReferenceTypeValue refVal => WriteToHeap(state, refVal, targetMember, value),
                ValueTypeValue valVal => WriteToStack(state, parentPath, valVal, targetMember, value),
                _ => state
            };

            static ProgramState WriteToHeap(ProgramState state, ReferenceTypeValue refVal, AccessSymbol targetMember, IAbstractValue valueToAssign)
            {
                // Reference Type -> Update Heap across all target allocation sites
                var currentHeap = state.HeapStore.ToBuilder();
                foreach (var site in refVal.Targets.Nodes)
                {
                    if (currentHeap.TryGetValue(site, out var heapNode))
                    {
                        var updatedHeapNode = heapNode.SetField(targetMember, valueToAssign);
                        currentHeap[site] = updatedHeapNode;
                    }
                }
                return new ProgramState(state.Environment, currentHeap.ToImmutable());
            }
            static ProgramState WriteToStack(ProgramState state, AccessPath parentPath, ValueTypeValue valVal, AccessSymbol targetMember, IAbstractValue valueToAssign)
            {
                var updatedFields = valVal.Fields.SetItem(targetMember, valueToAssign);
                var updatedVal = new ValueTypeValue(updatedFields);

                //Recursively write the update back up to its parent path
                return state.Write(parentPath, updatedVal);
            }
        }

        /// <summary>
        /// Invalidated all possible value for every nested field of the <paramref name="path"/>
        /// </summary>
        public static ProgramState InvalidateNested(this ProgramState state, AccessPath path, bool keepUntilFirstRevType = false)
        {
            var value = EvaluatePath(ref state, path);
            var updatedValue = InvalidateRecursive(value, ref state, keepUntilFirstRevType);
            return state.Write(path, updatedValue);

            static IAbstractValue InvalidateRecursive(IAbstractValue value, ref ProgramState state, bool keepUntilFirstRevType)
            {
                switch (value)
                {
                    case ValueTypeValue valVal:
                    {
                        if (valVal.Fields.IsEmpty && keepUntilFirstRevType)
                            return valVal;
                        var updatedFields = valVal.Fields;
                        foreach (var field in valVal.Fields)
                        {
                            var updatedVal = InvalidateRecursive(field.Value, ref state, keepUntilFirstRevType);
                            if (!keepUntilFirstRevType)
                                updatedFields = updatedFields.SetItem(field.Key, updatedVal);
                        }
                        return new ValueTypeValue(updatedFields);
                    }
                    case ReferenceTypeValue refVal:
                    {
                        foreach (var allocationNode in refVal.Targets.Nodes)
                        {
                            if (!state.HeapStore.TryGetValue(allocationNode, out var heapNode))
                                continue;
                            var updatedFields = heapNode.Fields;
                            foreach (var field in heapNode.Fields)
                            {
                                var updatedVal = InvalidateRecursive(field.Value, ref state, false);
                                updatedFields = updatedFields.SetItem(field.Key, updatedVal);
                            }
                            heapNode = new HeapNode(allocationNode, updatedFields);
                            state = state.WithHeap(allocationNode, heapNode);
                        }
                        return refVal;
                    }
                    default:
                        return value;
                }
            }
        }

        public static ProgramState AllocateOrWriteDefault(this ProgramState state, AccessPath path, ITypeSymbol objectType, IOperation? allocationOperation)
        {
            if (path.Members.IsEmpty && path.Root.Access?.Symbol is ILocalSymbol localSym)
                return state.Allocate(path.Root, localSym.Type, allocationOperation);
            var value = AllocateDefaultValueForType(ref state, objectType, allocationOperation);
            return state.Write(path, value);
        }

        public static IAbstractValue AllocateDefaultValueForType(ref ProgramState state, ITypeSymbol objectType, IOperation? allocationOperation)
        {
            if (objectType.IsValueType &&
                //Span is basically a wrapper around a point and therefore we need to copy by ref instead of by val
                !objectType.HasName("Span", "System"))
            {
                // Nested struct write -> x.Prop = new MyStruct();
                return allocationOperation == null
                     ? ValueTypeValue.Empty
                     : new ValueTypeValue(allocationOperation);
            }
            else
            {
                // Nested class write -> x.Prop = new MyClass();
                state = state.AllocateReferenceType(objectType, allocationOperation,
                    out var refVal);
                return refVal;
            }
        }
        private static ProgramState AllocateReferenceType(this ProgramState state, ITypeSymbol objectType, IOperation? allocationOperation,
            out ReferenceTypeValue refVal)
        {
            var site = new AllocationNode(state.HeapStore.Count + 1, objectType, allocationOperation);
            var heapNode = HeapNode.Create(site);
            refVal = new ReferenceTypeValue(PointsToSet.Empty.Add(site));
            return state.WithHeap(site, heapNode);
        }

        /// <summary>
        /// Evaluates a nested AccessPath (e.g., x.Prop1.Prop2) to retrieve its AbstractValue.
        /// Handles multiple parents/allocation sites automatically via set unioning.
        /// </summary>
        /// <remarks>
        /// Automatically adds missing parents from <paramref name="path"/>
        /// </remarks>
        public static IAbstractValue EvaluatePath(ref ProgramState state, AccessPath path)
        {
            if (!state.Environment.TryGetValue(path.Root, out var currentVal))
            {
                if (path.Root.IsStatic)
                {
                    //Allocate missing static-type
                    state = state.Allocate(path.Root, (ITypeSymbol)path.Root.Access.Value.Symbol!, path.Root.Access.Value.AccessOperation);
                    currentVal = state.Environment[path.Root];
                }
                else if (path.Root.Access?.AccessOperation?.Type is ITypeSymbol type)
                {
                    // Allocate missing root
                    state = state.Allocate(path.Root, type, path.Root.Access.Value.AccessOperation);
                    currentVal = state.Environment[path.Root];
                }
                else
                {
                    return ReferenceTypeValue.Empty;
                }
            }

            var currentPath = AccessPath.FromRoot(path.Root);
            foreach (var member in path.Members)
            {
                currentPath = currentPath.Append(member);
                currentVal = currentVal switch
                {
                    ReferenceTypeValue refVal => ResolveOrAddHeapMember(ref state, refVal, currentPath, member),
                    ValueTypeValue structVal => ResolveOrAddStackMember(ref state, structVal, currentPath, member),
                    _ => ReferenceTypeValue.Empty
                };
            }

            return currentVal;

            static IAbstractValue ResolveOrAddHeapMember(ref ProgramState state, ReferenceTypeValue refVal, AccessPath currentPath, AccessSymbol member)
            {
                IAbstractValue accumulated = ReferenceTypeValue.Empty;
                foreach (var site in refVal.Targets.Nodes)
                {
                    if (state.HeapStore.TryGetValue(site, out var heapNode) &&
                        heapNode.TryGetField(member) is IAbstractValue fieldVal)
                    {
                        //For invocations allocate a new reference and do not share existing references!!
                        if (member is { Symbol: IMethodSymbol })
                        {
                            if (member.AccessOperation == null)
                                goto ALLOCATE_NEW;
                            var knowAllocation = GetPossibleLastValues(fieldVal).ExactlyOneOrDefault();
                            if (!OperationEqualityComparer.Default.Equals(knowAllocation, member.AccessOperation))
                                goto ALLOCATE_NEW;
                            //We already allocated the exact same invocation operation
                            //Is this and only this case we can reuse the existing reference, because it comes from the excact same incovation
                        }
                        accumulated = accumulated.Merge(fieldVal);
                        continue;
                    }
                ALLOCATE_NEW:
                    var defaultValue = GetDefaultValueForMember(ref state, member);
                    state = state.Write(currentPath, defaultValue);
                    accumulated = accumulated.Merge(defaultValue);
                }
                return accumulated;
            }
            static IAbstractValue ResolveOrAddStackMember(ref ProgramState state, ValueTypeValue structVal, AccessPath currentPath, AccessSymbol member)
            {
                if (structVal.Fields.TryGetValue(member, out var val))
                    return val;
                var defaultValue = GetDefaultValueForMember(ref state, member);
                state = state.Write(currentPath, defaultValue);
                return defaultValue;
            }
            static IAbstractValue GetDefaultValueForMember(ref ProgramState state, AccessSymbol member)
            {
                // Prefer type of actual operation in case of casts
                var memberType = member.AccessOperation switch
                {
                    IConversionOperation cast => cast.Type,
                    _ => member.Symbol switch
                    {
                        IFieldSymbol field => field.Type,
                        IPropertySymbol property => property.Type,
                        IMethodSymbol method => method.ReturnType,
                        _ => null
                    }
                };
                if (memberType == null)
                    return ReferenceTypeValue.Empty;
                return AllocateDefaultValueForType(ref state, memberType, member.AccessOperation);
            }
        }

        /// <summary>
        /// Returns the stored possible values of the given <paramref name="value"/>
        /// </summary>
        public static IEnumerable<IOperation?> GetPossibleLastValues(IAbstractValue value)
        {
            switch (value)
            {
                case ValueTypeValue valVal:
                {
                    foreach (var possibleValue in valVal.PossibleValues.Distinct(OperationEqualityComparer.Default))
                    {
                        yield return possibleValue;
                    }
                    yield break;
                }
                case ReferenceTypeValue refVal:
                {
                    foreach (var allocationOperation in refVal.Targets.Nodes.Select(x => x.AllocationOperation).Distinct(OperationEqualityComparer.Default))
                    {
                        yield return allocationOperation;
                    }
                    yield break;
                }
                default:
                    yield break;
            }
        }

    }
}
