This code convention is inspired and tweak from Epic Games Unreal Engine Code Standard at https://dev.epicgames.com/documentation/unreal-engine/epic-cplusplus-coding-standard-for-unreal-engine

# Copyright Notice

Any source file (.h, .cpp, .xaml) provided by Epic Games for public distribution must contain a copyright notice as the first line in the file. The format of the notice must exactly match that shown below:

```cpp
// Copyright CreationArtStudios and eriryukhai. All Rights Reserved.
```

# Naming Conventions

When using Naming Conventions, all code and comments should use U.S. English spelling and grammar.
- The first letter of each word in a name (such as type name or variable name) is capitalized. There is usually no underscore between words. For example, `Engine` and `GameEngine` or `EditorEngine` are correct but `engine`, `gameEngine` or `editorEngine` are not
- For variables, the `m_` conventions are discouraged.
- Template classes are prefixed by T.
```cpp
template <typename ObjectType>
      class TFoo
```
- Classes that are abstract interfaces are prefixed by I.
```cpp
class IFoo
```
- Structs are prefixed by S
  ```cpp
  struct SFoo
  {
  }
  ```
- Enums are prefixed by E
```cpp
enum class EColorBits
{
};
```
- Boolean variables must be prefixed by b.
  ```cpp
  bFoo = false;
  ```
  - Macro names should be fully capitalized with words separated by underscores, and prefixed with `ME_ .

```cpp
#define ME_EXAMPLE_DEFINE
```

Below conventions is taken 1:1 from Epic Games Unreal Engine Code Standard for comments with minor modifications.

## Comments
- You can use multi-line or single-line comments before a variable Blank lines are optional for grouping variables.

- All functions that return a bool should ask a true/false question, such as `IsVisible()` or `ShouldClearBuffer()`.

- A procedure (a function with no return value) should use a strong verb followed by an Object. An exception is, if the Object of the method is the Object it is in. In this case, the Object is understood from context. Names to avoid include those beginning with "Handle" and "Process" because the verbs are ambiguous.

- We encourage you to prefix function parameter names with "Out" if:

1. The function parameters are passed by reference.
2. The function is expected to write to that value.

This makes it obvious that the value passed in this argument is replaced by the function.

If an In or Out parameter is also a boolean, put "b" before the In/Out prefix, such as `bOutResult`.

Functions that return a value should describe the return value. The name should make clear what value the function returns. This is particularly important for boolean functions. Consider the following two example methods:

```cpp
// what does true mean?
bool CheckTea(FTea Tea); 
// name makes it clear true means tea is fresh
bool IsTeaFresh(FTea Tea); 
float TeaWeight;
uint32_t TeaCount;
bool bDoesTeaStink;
```
### Guidelines

- Write self-documenting code. For example:
```cpp
// Bad:
t = s + l - b;		    
// Good:
TotalLeaves = SmallLeaves + LargeLeaves - SmallAndLargeLeaves;
```
- Write useful comments. For example:
```cpp
// Bad:
// increment Leaves!    
++Leaves;

// Good:
// we know there is another tea leaf    
++Leaves;
```
    
- Do not over comment bad code — rewrite it instead. For example:
```cpp
// Bad:
// total number of leaves is sum of
// small and large leaves less the
// number of leaves that are both    
t = s + l - b;		    

// Good:
TotalLeaves = SmallLeaves + LargeLeaves - SmallAndLargeLeaves;
```
    
- Do not contradict the code. For example:
```cpp
// Bad:
// never increment Leaves!
++Leaves;

// Good:
// we know there is another tea leaf
++Leaves;
```
# Use of standard libraries
For the engine, it is recommended to make a wrapper for the standard libraries like c++ standard library or third-party libraries.

When there is a choice between a standard library feature instead of our own, you should prefer the option that gives superior results. It's also important to remember that consistency is valued.

Avoid mixing internal  idioms and standard library idioms in the same API. The following table lists common idioms along with recommendations on when to use them.
# Modern C++ Language Syntax
## Override and Final

The `override` and `final` keywords are valid for use, and their use is strongly encouraged. There might be many places where these have been omitted, but they will be fixed over time.
## Nullptr

You should use `nullptr` instead of the C-style `NULL` macro in all cases.
## Auto

You shouldn't use `auto` in C++ code, except for the few exceptions listed below. Always be explicit about the type you're initializing. This means that the type must be plainly visible to the reader.
It's very important that types are clearly visible to someone who is reading the code. Even though some IDEs are able to infer the type, doing so relies on the code being in a compilable state. It also won't assist users of merge/diff tools, or when viewing individual source files in isolation, such as on GitHub.
## Default Member Initializers

Default member initializers can be used to define the defaults of a class inside the class itself:
```cpp
class FooClass : public BaseFoo
{
public:
float FooFloat = 1.0f;
bool FooBool = false;
}
```
# Code Formatting

## Braces

Avoid brace conflicts in . `.h` and`cpp` file. Please adhere to this usage, regardless of the size of the function or block. For example:

```cpp
// Bad
int32 GetSize() const { return Size; } 

// Good
int32 GetSize() const
{    
	return Size;
}
```
The only acceptable usage of this is to make a `.inl` for inline and added it at the very bottom of the header file.
## Function parameters
Avoid overly-long function parameter lists.

- If a function takes many parameters then consider passing a dedicated struct instead:
```cpp
// Old style
std::string_view FuncReturningStrView(foo parameter1, foo1 parameter2, foo2 parameter 3, foo4 parameter 4, foo5 parameter5)

// Recommended style
struct SFooStruct
{
	foo parameter1;
	foo1 parameter2;
	foo2 parameter3;
	foo4 parameter4;
	foo5 parameter5;
}
std::string_view FuncReturningStrView(SFooStruct FooStruct);
```
## Override and Virtual keywords
Use the `virtual` and `override` keywords when declaring an overriding method.

When declaring a virtual function in a derived class that overrides a virtual function in the parent class, you must use both the `virtual` and the `override` keywords. For example:
# Tabs and Indenting

Below are some standards for indenting your code.

- Indent code by execution block.
- Use tabs for whitespace at the beginning of a line, not spaces. Set your tab size to 4 characters. Note, spaces are sometimes necessary and allowed for keeping code aligned regardless of the number of spaces in a tab. For example, when you are aligning code that follows non-tab characters.
# Switch Statements

Except for empty cases (multiple cases having identical code), switch case statements should explicitly label that a case falls through to the next case. Either include a break, or include a "falls through" comment in each case. Other code control-transfer commands (return, continue, and so on) are fine as well.

Always have a default case. Include a break just in case someone adds a new case after the default.
```cpp
switch (condition)
{    
	case 1:        
	...        
	// falls through     
	case 2:        
	...        
	break;   
	  
	case 3:        
	...        
	return; 
	    
	case 4: 
	   
	case 5:        
	...        
	break;   
	  
	default:        
	break;
}
```
# Encapsulation

Enforce encapsulation with the protection keywords. Class members should almost always be declared private unless they are part of the public/protected interface to the class. Use your best judgment, but always be aware that a lack of accessors makes it hard to refactor later without breaking plugins and existing projects.

If particular fields are only intended to be usable by derived classes, make them private and provide protected accessors.

Use final if your class is not designed to be derived from.

If a function or class members need to be accessed or mutate by others, make an accessors or mutators.