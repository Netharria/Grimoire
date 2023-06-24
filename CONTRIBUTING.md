# Contributing to Grimoire

Hi! First of all, thanks for your interest in contributing. Help in various forms is almost always
needed and in any case appreciated.

## Overview

Grimoire is written in [.NET 7] with the [DSharpPlus] commands framework. For dependency management we
use [nuget]. 

## Getting Started: Coding

If you want to add a new feature, please start out by creating an issue and opening a discussion
about it. Pull Requests coming out of the blue won't be accepted. If you want to pick up an existing
issue for implementation, leave a note there that you're doing so, so that no one else starts on the
same work as you do accidentally. Then, you can start actually working on it:

## Code Changes

If you want to make changes to Grimoire, start by creating a fork. Make your changes in your fork and when you are done create a pull request. Pull requests must target the latest relevant branch. Pull requests that are not up to date will not be approved. Please include details on any issues the code change is resolving or new features being added.

## Code Style

A `./editorconfig` has been setup and it is expected that all its rules have been followed. A few preferences are listed below. (Generated code is not included in this.)

* Use class initializer syntax when possible.
* When working with async code, and your method consists of a single await statement not in any if, while, etc. blocks, pass the task through instead of awaiting it. 


[DSharpPlus]: https://github.com/DSharpPlus/DSharpPlus/
[.NET 7]: https://dotnet.microsoft.com/en-us/download/dotnet/7.0
[nuget]: https://www.nuget.org/
