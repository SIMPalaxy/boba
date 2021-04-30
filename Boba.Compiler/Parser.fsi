// Signature file for parser generated by fsyacc
module Parser
type token = 
  | UNTAG
  | PUT_REF
  | GET_REF
  | NEW_REF
  | WITH_STATE
  | FUNCTION
  | LOCAL
  | LET
  | PATTERN
  | RECURSIVE
  | DATA
  | MAIN
  | EXPORT
  | AS
  | IMPORT
  | EQUALS
  | ELLIPSIS
  | BAR
  | DOT
  | PLUS
  | MINUS
  | COLON
  | DOUBLE_COLON
  | SEMICOLON
  | L_ARROW
  | R_ARROW
  | L_BRACKET
  | R_BRACKET
  | L_BRACE
  | R_BRACE
  | L_PAREN
  | R_PAREN
  | L_ANGLE
  | R_ANGLE
  | STRING of (StringLiteral)
  | DECIMAL of (DecimalLiteral)
  | INTEGER of (IntegerLiteral)
  | PREDICATE_NAME of (Name)
  | OPERATOR_NAME of (Name)
  | BIG_NAME of (Name)
  | SMALL_NAME of (Name)
type tokenId = 
    | TOKEN_UNTAG
    | TOKEN_PUT_REF
    | TOKEN_GET_REF
    | TOKEN_NEW_REF
    | TOKEN_WITH_STATE
    | TOKEN_FUNCTION
    | TOKEN_LOCAL
    | TOKEN_LET
    | TOKEN_PATTERN
    | TOKEN_RECURSIVE
    | TOKEN_DATA
    | TOKEN_MAIN
    | TOKEN_EXPORT
    | TOKEN_AS
    | TOKEN_IMPORT
    | TOKEN_EQUALS
    | TOKEN_ELLIPSIS
    | TOKEN_BAR
    | TOKEN_DOT
    | TOKEN_PLUS
    | TOKEN_MINUS
    | TOKEN_COLON
    | TOKEN_DOUBLE_COLON
    | TOKEN_SEMICOLON
    | TOKEN_L_ARROW
    | TOKEN_R_ARROW
    | TOKEN_L_BRACKET
    | TOKEN_R_BRACKET
    | TOKEN_L_BRACE
    | TOKEN_R_BRACE
    | TOKEN_L_PAREN
    | TOKEN_R_PAREN
    | TOKEN_L_ANGLE
    | TOKEN_R_ANGLE
    | TOKEN_STRING
    | TOKEN_DECIMAL
    | TOKEN_INTEGER
    | TOKEN_PREDICATE_NAME
    | TOKEN_OPERATOR_NAME
    | TOKEN_BIG_NAME
    | TOKEN_SMALL_NAME
    | TOKEN_end_of_input
    | TOKEN_error
type nonTerminalId = 
    | NONTERM__startunit
    | NONTERM_unit
    | NONTERM_import_list
    | NONTERM_decl_list
    | NONTERM_main
    | NONTERM_import
    | NONTERM_import_path
    | NONTERM_remote
    | NONTERM_export
    | NONTERM_brace_names
    | NONTERM_name_list
    | NONTERM_name
    | NONTERM_declaration
    | NONTERM_term_statement_block
    | NONTERM_term_statement_list
    | NONTERM_term_statement
    | NONTERM_local_function_list
    | NONTERM_local_function
    | NONTERM_simple_expr
    | NONTERM_word
    | NONTERM_identifier
    | NONTERM_qualified_name
    | NONTERM_pattern_expr_list
    | NONTERM_pattern_expr
    | NONTERM_fixed_size_term_expr
    | NONTERM_fixed_size_term_factor_list
    | NONTERM_fixed_size_term_factor
/// This function maps tokens to integer indexes
val tagOfToken: token -> int

/// This function maps integer indexes to symbolic token ids
val tokenTagToTokenId: int -> tokenId

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
val prodIdxToNonTerminal: int -> nonTerminalId

/// This function gets the name of a token as a string
val token_to_string: token -> string
val unit : (FSharp.Text.Lexing.LexBuffer<'cty> -> token) -> FSharp.Text.Lexing.LexBuffer<'cty> -> ( Unit ) 
